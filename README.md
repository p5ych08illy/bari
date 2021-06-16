[bari](http://vigoo.github.io/bari/)
====
 
[Bari](http://vigoo.github.io/bari/) is an advanced build management tool for .NET projects.

![CI](https://github.com/vigoo/bari/workflows/CI/badge.svg)
[![Apache 2 License License](http://img.shields.io/badge/license-APACHE2-blue.svg)](http://www.apache.org/licenses/LICENSE-2.0)

# Getting started #
## Getting bari ##

Bari itself is now compiled using bari, so you'll have to download the latest version as a binary package. 

The [latest released version is 1.0.1](https://github.com/vigoo/bari/releases/tag/1.0.1).

To use the latest build, install [bari from NuGet](https://www.nuget.org/packages/bari).

To upgrade an existing version use `bari selfupdate`.

## Documentation ##
Documentation is under construction and available from the [getting started page](https://github.com/vigoo/bari/wiki/GettingStarted).

Additionally I recommend browsing the following suite definitions for examples:

* [Simple C# executable](https://github.com/vigoo/bari/blob/master/systest/single-cs-exe/suite.yaml)
* [Simple F# executable](https://github.com/vigoo/bari/blob/master/systest/single-fs-exe/suite.yaml)
* [Simple C++ executable](https://github.com/vigoo/bari/tree/master/systest/single-cpp-exe)
* [Dependencies within a module](https://github.com/vigoo/bari/blob/master/systest/module-ref-test/suite.yaml)
* [Dependencies within a suite](https://github.com/vigoo/bari/blob/master/systest/suite-ref-test/suite.yaml)
* [Content files support](https://github.com/vigoo/bari/blob/master/systest/content-test/suite.yaml)
* [Support for reference aliases](https://github.com/vigoo/bari/blob/master/systest/alias-test/suite.yaml)
* [File system repository](https://github.com/vigoo/bari/blob/master/systest/fsrepo-test/suite.yaml) 
* [C++ static library support](https://github.com/vigoo/bari/blob/master/systest/static-lib-test/suite.yaml)
* [C++ resource support](https://github.com/vigoo/bari/blob/master/systest/cpp-rc-support/suite.yaml)
* [C++/CLI support](https://github.com/vigoo/bari/blob/master/systest/mixed-cpp-cli/suite.yaml)
* [Registration-free COM support](https://github.com/vigoo/bari/blob/master/systest/regfree-com-server/suite.yaml)
* [Postprocessor scripts](https://github.com/vigoo/bari/blob/master/systest/postprocessor-script-test/suite.yaml)
* [Custom plugin support](https://github.com/vigoo/bari/blob/master/systest/custom-plugin-test/suite.yaml)

And the [Bari suite definition](https://github.com/vigoo/bari/blob/master/suite.yaml) itself! 

To get a list of available commands, use
`bari help`.

## Visual Studio add-on

A Visual Studio add-on for bari [is also in development](https://github.com/zvrana/bari-vs-addon). 
