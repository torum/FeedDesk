# FeedDesk


A desktop feed reader. (work in progress)

## Download app
from [Microsoft Store](https://www.microsoft.com/store/apps/9PGDGKFSV6L9).

## Source code
Source code is located at [BlogWrite](https://github.com/torum/BlogWrite) repo.

## Features
### Implements following formats:  
* Atom 0.3
* [The Atom Syndication Format](https://tools.ietf.org/html/rfc4287) (Atom 1.0)
* [RDF Site Summary](https://www.w3.org/2001/09/rdfprimer/rss.html) (RSS 1.0)
* [Really Simple Syndication](https://validator.w3.org/feed/docs/rss2.html) (RSS 2.0)

### Supported extentions:
* media
* hatena
* dc
* iTunes

### Other features:
* Feed Autodiscovery.
* OPML import, export.
* Display enclosed or embeded images.
* Podcasts audio and in-app playback.

## Screenshots:

![FeedDesk](https://github.com/torum/BlogWrite/blob/master/docs/images/FeedDesk-Screenshot1-Dark.png?raw=true) 

![FeedDesk](https://github.com/torum/BlogWrite/blob/master/docs/images/FeedDesk-Screenshot1-Light.png?raw=true) 

![FeedDesk](https://github.com/torum/BlogWrite/blob/master/docs/images/FeedDesk-Screenshot1-Dark-Text.png?raw=true) 

![FeedDesk](https://github.com/torum/BlogWrite/blob/master/docs/images/FeedDesk-Screenshot1-Dark-Podcast.png?raw=true) 

![FeedDesk](https://github.com/torum/BlogWrite/blob/master/docs/images/FeedDesk-Screenshot1-Acrylic-Dark.png?raw=true) 

![FeedDesk](https://github.com/torum/BlogWrite/blob/master/docs/images/FeedDesk-Screenshot1-Acrylic-Light.png?raw=true) 

![FeedDesk](https://github.com/torum/BlogWrite/blob/master/docs/images/FeedDesk-Screenshot1-Light-vertical.png?raw=true) 

# Contributing
Feel free to open issues and send PRs. 

# Technologies, Frameworks, Libraries
* [.NET 6](https://github.com/dotnet/runtime)  
* [WinUI3 (Windows App SDK)](https://github.com/microsoft/WindowsAppSDK) 
* [Community Toolkit](https://github.com/CommunityToolkit) 
* [WinUIEx](https://github.com/dotMorten/WinUIEx)
* [SQLite](https://github.com/sqlite/sqlite) ([System.Data.SQLite](https://system.data.sqlite.org/index.html/doc/trunk/www/index.wiki))

# Getting Started

## Requirements
* Windows 10.0.19041.0 or higher

## Building
1. Visual Studio 2022 with support for .NET Desktop App UI development (and optionally .NET Universal App development)
2. Clone [BlogWrite](https://github.com/torum/BlogWrite) repository
3. Open solution in Visual Studio
4. Select FeedDesk project, then compile and run.
