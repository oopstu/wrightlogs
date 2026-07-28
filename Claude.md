Log Viewer for MacOS and Decisions.

# App Purpose
This application is a dotnet app using Avalonia UI framework.  It will be a desktop client for log analysis and log viewing.  The logs are from a known application and have a few types.  

## File Management Note
The files can be very large (30-50MB at a time) so I Want to the file to be virtually loaded into the client and more data fetched from the file as the log view area scrolls.  

# Application Details
## UI Layout
The application should have a typical toolbar along the top to open a different file, refresh the file.

## Log File Types
The file types are:
1. Log Files
2. Usage Files
3. Usage Detail Files

### 1. Log Files
The main log file is called “Decisions.Web.Core.log” and is a JSON formatted file.  This file is a typical log file emitted from Serilog utilities.  It has 
- log id
- Date time
- Thread id
- Message
- Level

These files are the most important because they are detailed application logs.  There will be filtering, searching, live updates, and other features added in the future to fully support the view and analysis of these files.

#### Properties field
Most lines also have a JSON `Properties` object with a handful of additional metadata elements. Not every line has `Properties`, and not every `Properties` object has every element — these are optional/sparse. The key sub-properties that matter for the viewer are:
- `LogNumber`
- `ThreadId`
- `InstanceName`
- `SessionId`
- `Category`

These five should each be their own column in the log viewer (rather than buried in a raw properties blob), with `LogNumber` as the first column. A row missing `Properties` entirely, or missing one of these specific elements, should just show that column blank for that row — not a placeholder/zero/error.

### 2. Usage Files
Usage files are CSV files that show counts of specific events every 30 seconds.  These files are helpful for finding trends and problems in an environment. It is like “Heartbeat data”.  With this data, creating graphs against the data is going to be the main way we help the user analyze what’s happening in the customer environment.  


### 3. Usage Detail Files
Usage detail files roll over very quickly.  These show specific process names and rule names that run at specific times.  These files are usually not helpful unless a user is looking at something in a “Usage” file as described above in 2.  Our tool will allow us to scan a directory of files and graph usage files from #2.  Then if there are “details” available for a timestamp in #2 we can find and load this detailed data on demand.  
 
# Examples
Example files can be found in the folder "examples" and are as follows:
 1. Decisions.Web.Core.1.log - roll over or "older" version of main log file which is indicated by the index between "Core" and "log"
 2. Decisions.Web.Core.log - most recent main log file
 3. Decisions.Web.Core.Usage.1.csv - roll over of "older" usage data indicated by index number between "Usage" and "csv"
 4. Decisions.Web.Core.Usage.csv - Usage file as described in #2 - usage files
 5. Decisions.Web.Core.UsageDetails.1.txt - roll over of detail file as indicated by index between "UsageDetails" and "txt"
 6. Decisions.Web.Core.UsageDetails.txt - Most recent detail file as described in #3. 
