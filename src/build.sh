#!/bin/bash

DESTINATION_DIR="/usr/local/bin"

# Build
dotnet build

# Publish the application
dotnet publish -r linux-x64 -p:PublishSingleFile=true --self-contained false -c Release

# Copy the application to specified destination
sudo cp bin/Release/net6.0/linux-x64/publish/Yanta $DESTINATION_DIR
echo ">> Moved binary to $DESTINATION_DIR"
