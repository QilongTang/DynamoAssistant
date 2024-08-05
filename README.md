[![Build](https://github.com/QilongTang/DynamoAssistant/actions/workflows/build.yml/badge.svg)](https://github.com/QilongTang/DynamoAssistant/actions/workflows/build.yml)

![Image](https://raw.github.com/ikeough/Dynamo/master/doc/distrib/Images/dynamo_logo_dark.png)

# Dynamo Assistant
Gen-AI based Dynamo Assistant
![Image](https://global.discourse-cdn.com/business6/uploads/dynamobim/original/3X/3/2/328a54785c48ce28bb56999e894cea192b27f7de.jpeg)

This Dynamo view extension make use of the [OpenAI .NET SDK](https://www.nuget.org/packages/OpenAI/2.0.0-beta.8) and [Dynamo NuGet packages](https://www.nuget.org/packages?q=DynamoVisualProgramming). NuGet should take care of restoring these packages if they are not available on your system at build time.

# Building the Assistant

## Requirements

- Visual Studio 2022
- .NET Framework 8

## Instructions

- Clone the repository.
- Choose the `main` branch:
- Open `DynamoAssistant.sln` with Visual Studio 2022.
- Build using the `Debug/Any CPU` configuration.
- The `dynamo_viewExtension` folder at the root of the repository will now have the built libraries. The `dynamo_viewExtension` folder in that directory can be copied directly to your Dynamo packages directory:`C:\Users\<you>\AppData\Roaming\Dynamo Core\<version>\packages`.
- Run Dynamo Assistant. You should find `Dynamo Gen-AI assistant` under `Extensions` tab inside of Dynamo.
