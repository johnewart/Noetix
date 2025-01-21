cd pkg 
for i in *; do 
  dotnet nuget push $i --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json 
done 
