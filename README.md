# yanta
Yet Another Note-Taking App. Built using C# &amp; Gtk. Primarily built for Linux.

Note: This app is within its very early stages, with a lot more features planned in the near future. \
**Windows is not *currently* supported yet.** Or, rather, I have not tested this on Windows.

## Showcase
Image of Yanta program using the Nord theme. <br/><br/>
<a href="https://raw.githubusercontent.com/Snoozeds/yanta/main/showcase/images/1.png" target="_blank"><img src="/showcase/images/1.png" alt="Image of Yanta program using the Nord theme." width="500"/></a>

# Installation
You can download pre-built binaries in [Releases](https://github.com/Snoozeds/yanta/releases), or manually build using the instructions below.

## Manually building
Requires [.NET 6.0+](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

### Shell script:
**Sudo is usually required to move the built binary to `/usr/local/bin`.** <br />
You may also change the DESTINATION_DIR variable within the sh file.
```
git clone https://github.com/Snoozeds/yanta.git
cd yanta/src
chmod +x build.sh
sudo ./build.sh
```

### Commands:
```
git clone https://github.com/Snoozeds/yanta.git
cd yanta/src

dotnet build
dotnet publish -r linux-x64 -p:PublishSingleFile=true --self-contained false -c Release

# Move the binary to /usr/local/bin or whatever you want to do with it.
# sudo cp bin/Release/net6.0/linux-x64/publish/Yanta /usr/local/bin
```
