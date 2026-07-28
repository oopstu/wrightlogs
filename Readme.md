John Lloyd Wright, inventor of Lincon Logs, son of famous architect. 
The Right logs help us diagnose and correct issues.
We write logs to make our systems maintainable.  
Richard Wright was a good buddy.  
I like puns, welcome to WrightLogs. 

# Wright Logs
A tool for quickly processing Decisions logs which are broken down into three main groups:
1. Main Application Logs (Web.Core.X.txt)
2. Usage Logs (Metrics stored locally through Open Telemetry)
3. Usage Detail Logs which show some detailed information about the items reported in #2.


# Use
Run it.  Pick a folder.  Select logs from the drop down.  Be patient if you're asking it to show "All Usage" because it has to work to aggregate all the metrics. 

# Issues
Just fix them!

# Making it available! 
Building the application should create a zip and upload it to whqnextcloud.  This is a file share system
setup and maintained by IT.  You need access to it and then you MUST configure an environment variable called "NEXT_CLOUD_CREDS" or the upload part will fail.  You can ignore that safely if you want and just use this locally.

`Just use "./build.sh win|mac" to build the app and upload it to the site where it can auto update!`