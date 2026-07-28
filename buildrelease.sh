#! /bin/zsh
# In order to use this you need to have an environment variable
#  called NEXT_CLOUD_CREDS

if [ -z "$1" ]; then
  echo "You must run ./build.sh [win|mac] to use this script.  You didn't specify an OS, c'mon doofus!"
  exit 1
fi

if [[ "$1" == "win" ]]; then
   RELEASECONFIG="win-x64"
elif [[ "$1" == "mac" ]]; then
   RELEASECONFIG="osx-arm64"
else
   echo "Use only 'win' or 'mac' stupid.  Not hard."
   exit 2
fi

OUTPUT_FOLDER=out$1
PROJECT_FILE="WrightLogs.csproj"

: "${NEXT_CLOUD_CREDS:?Error: You MUST have a NEXT_CLOUD_CREDS environment variable set to use this script. Exiting.}"


#Extract Assembly Version
VERSION=$(grep -E -m 1 -o "<(AssemblyVersion|Version)>[^<]+" "$PROJECT_FILE" | sed -r 's/<[^>]+>//')

if [ -n "$VERSION" ]; then
    echo "Assembly Version: $VERSION"
else
    echo "Version tag not found in $PROJECT_FILE."
    exit 1;	
fi

rm -rf $OUTPUT_FOLDER/*

dotnet publish -c Release -r $RELEASECONFIG -o $OUTPUT_FOLDER --self-contained true -p:PublishSingleFile=true
cd $OUTPUT_FOLDER 
zip wrightlogs.$1.$VERSION.zip *
cd ..

echo "Uploading $1 release package: $OUTPUT_FOLDER/wrightlogs.$1.$VERSION.zip"
curl -# -u $NEXT_CLOUD_CREDS -T $OUTPUT_FOLDER/wrightlogs.$1.$VERSION.zip https://whqnextcloud.decisions.com/remote.php/webdav/Product%20Engineering/DimwitReleases/ 
