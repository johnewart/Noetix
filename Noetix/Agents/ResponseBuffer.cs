using System.Text;
using System.Text.RegularExpressions;

namespace Noetix.Agents;

public record StartStopToken(string StartToken, string StopToken);

public class ResponseBuffer
{
    private readonly StringBuilder _buffer = new();
    private readonly Action<string> _streamHandler;
    private readonly List<StartStopToken> _tokenPairs = new();
    private StartStopToken? _currentTokenPair;
    
    public ResponseBuffer(Action<string> streamHandler)
    {
        _streamHandler = streamHandler;
    }

    public void AddToken(string token)
    {
        _buffer.Append(token);
        if (_buffer.Length > 100)
        {
            ProcessBuffer();
        }
    }

    
    
    private void ProcessBuffer()
    {
        var content = _buffer.ToString();

        if (_currentTokenPair != null)
        {
            // If we are currently processing a token pair, continue to buffer the content until the stop token is found
            var stopToken = _currentTokenPair.StopToken;
            var stopIndex = content.IndexOf(stopToken, StringComparison.Ordinal);
            if (stopIndex >= 0)
            {
                _buffer.Clear();
                _buffer.Append(content.Substring(stopIndex + stopToken.Length));
                _currentTokenPair = null;
            }
            
            return;
        }

        // Check if any of the patterns are found in the content
        foreach (var tokenPair in _tokenPairs)
        {
            var startToken = tokenPair.StartToken;
            var stopToken = tokenPair.StopToken;
            var pattern = $"{startToken}.*?{stopToken}";
            var matches = Regex.Matches(content, pattern, RegexOptions.Singleline);
            if (matches.Count > 0)
            {
                _currentTokenPair = tokenPair;

                var match = matches[0];
                var start = match.Index;
                var preMatchContent = content.Substring(0, start);
                var matchContent = content.Substring(start);
                // Flush the content before the start token
                FlushStream(preMatchContent);
                _buffer.Clear();
                // Append the content including the start token so that the stop token can be found 
                // if it is in the same buffer
                _buffer.Append(matchContent);
                return;
            }
        }

        // If no patterns are found, stream the content as is
        _streamHandler(_buffer.ToString());
        _buffer.Clear();
    }

    // Simulate a streaming response by flushing the buffer and delaying for a short period 
    private void FlushStream(string content)
    {
        // Sleep for a random short period to simulate streaming
        foreach (var token in content.Split())
        {
            _streamHandler(token);
            Thread.Sleep(new Random().Next(100, 500));
        }
    }
}