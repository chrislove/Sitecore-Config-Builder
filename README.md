# Sitecore ConfigBuilder

This is an instrument to get all your Sitecore configuration files and output single summarized configuration file that will be actually used by Sitecore CMS. It also can merge it with ASP.NET sections of web.config, and also sort all XML elements to make them easier to read and compare (soring is done only when it does not change sequence of handlers, pipeline processors etc.). 

New feature of 1.4 is downloading default configuration for the specific Sitecore version for comparison with the actual.

### [Downloads](https://github.com/Sitecore/Sitecore-Config-Builder/releases)

### Details

* It takes `web.config` file and `App_Config` folder to create `showconfig.xml` and/or `web.config.result.xml` files.
* It is like an `sitecore/admin/showconfig.aspx` page but doesn't need a Sitecore instance to be installed and run.
* The `web.config.result.xml` file is made by merging web.config and showconfig.xml files.
* It can do normalization of both files (reordering configuration sections when it doesn't change execution logic)
* The normalized versions of `web.config.result.xml` files are perfect for comparing 

### Open Source

The sources are open for inspection by everybody, please fork and improve if you like. 

### Use its API

This repository contains only UI part of the application, the engine is available as Sitecore.Diagnostics.ConfigBuilder NuGet package.
