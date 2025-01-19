using Noetix.Agents;
using Noetix.Agents.Storage;
using Noetix.Knowledge;
using Noetix.Knowledge.SharpVector;
using Noetix.Knowledge.Storage;
using Noetix.Knowledge.Tools;
using Noetix.Anthropic;
using Noetix.LLM.Common;

namespace ResearchAssistant;

using Noetix.Agents.Tools;

internal static class Program
{
    static async Task Main(params string[] args)
    {
       var toolStatusHandler = new ToolStatusCallback(update => {
            Console.WriteLine($"Status update: {update.ToolId}/{update.State} - {update.Message}");
        });
        
        var wikipediaSearchTool = new WikipediaSearchTool();
        
        var anthropicProvider = new AnthropicLLM(new AnthropicLLM.AnthropicConfig
        {
            ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        });
        var anthropicModel = "claude-3-5-sonnet-latest";
        var vectorDb = new SharpVector();
        
        Directory.CreateDirectory("data");
        var fileStore = new FileDocumentStore("data");
        var knowledgeBase = new Knowledgebase(fileStore, vectorDb);
        vectorDb.IndexAll(fileStore.Documents());
        
        var searchTool = new SearchKnowledgebaseTool(knowledgeBase);
        var updateTool = new UpdateKnowledgebaseTool(knowledgeBase);
        var tools = new List<AssistantTool> { wikipediaSearchTool, updateTool, searchTool };
        
        foreach(var t in tools)
        {
            t.StatusCallbacks = new List<ToolStatusCallback>{ toolStatusHandler };
        }
        
        var chatHistoryStore = new InMemoryChatHistoryStore();
        var assistant = new Assistant(
            model: anthropicModel,
            name: "Research Assistant",
            llm: anthropicProvider,
            tools: tools,
            greeting:
            "Hello, I'm the Research Assistant. I can help you find information on Wikipedia. You can ask me to search for articles with phrases like 'Search for the top 3 articles on Formula1 Racing', or 'Tell me the population of the various boroughs of New York City.'",
            description: "You are an expert AI research assistant - your job is to find the most relevant information for the user's query and provide analysis of the data you find.", 
            instructions:
            """
            Always use the tools you have to search for the most up to date information (unless you are being asked something about yourself - such as your name, what tools you have, etc.).
            Always search the knowledgebase first before using external tools.
            If you need to do additional research, you can use the tools you have to help you. 
            Your job is to distill the information you find in Wikipedia to answer the user's research question. 
            Use your analytical skills to look at the information provided and come up with a useful answer. 
            If you feel that you cannot answer the question well, ask the user for followup criteria or information. 
            Store relevant information in the knowledgebase for future reference to reduce reliance on external data.
            """,
            chatHistoryStore: chatHistoryStore,
            onStatusUpdate: message =>
            {
                Console.WriteLine($"Status update: {message.AssistantName}/{message.Kind} - {message.Message}");  
            },
            defaultGenerationOptions: new GenerationOptions
            {
                Temperature = 1.2,
                TopK = 50,
                TopP = 0.8,
            }
        );

        var intro = """
                    Ask me a question or give me a task, or type 'exit' to quit.
                    For example, you can ask me to:
                    * Search for articles on a topic by typing 'Search for articles on Formula 1 Racing'
                    * Extract and distill some information 'Tell me about the success of Ferrari in Formula 1 Racing in 2024'
                    * Explicitly search existing knowledge 'Search the knowledgebase for articles on Formula 1 Racing'
                    * Update the knowledgebase with new information 'Update the knowledgebase with the latest information on Formula 1 Racing'
                    """;
        
        Console.WriteLine();
        Console.WriteLine(assistant.Greeting.Content);
        Console.WriteLine(intro);
        

        while (true)
        {
            Console.WriteLine();
            Console.Write("> ");
            var input = Console.ReadLine();
            if (input == "knowledge")
            {
                var docs = knowledgeBase.Documents();
                foreach (var doc in docs)
                {
                    Console.WriteLine($"* {doc.Name}");
                }
            }

            if (input == "search")
            {
                Console.WriteLine("Enter a search query:");
                var query = Console.ReadLine();
                var searchResult = await knowledgeBase.Search(query, 10);
                foreach (var doc in searchResult)
                {
                    Console.WriteLine($"* {doc.Name}");
                }
            }
            else
            {
                var response = await assistant.Chat(new UserMessage(input));
                Console.WriteLine(response.Content);
                Console.WriteLine();
            }

        }
    }
}