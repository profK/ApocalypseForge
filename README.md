# ApocalypseForge
A discord bot for Forged in the Apocalypse games written in F#

Note that it expects your discordbot token to be in an environment variable calles TOKEN
I launch it using a shell script that sets the token and then starst the bot:
```
#!/bin/sh
export TOKEN="YOURTOKEN" 
cd apocalypse_forge
dotnet ApocalypseForge.dll
```
