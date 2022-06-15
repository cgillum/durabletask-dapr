# Dapr Provider for the Durable Task Framework

This repo contains the code for a [Durable Task Framework](https://github.com/Azure/durabletask) .NET-based storage provider that uses [Dapr Actors](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/) for state storage and scheduling. It's meant to be used as a proof-of-concept / reference implementation for the embedded engine described in the [Dapr Workflow proposal](https://github.com/dapr/dapr/issues/4576).

> **IMPORTANT** This code is NOT going to be used in any way as the actual Dapr Workflow implementation. The "real" implementation will be done in Go so that it can be embedded into the Dapr sidecar. This project is just a reference implementation to help us tackle some of the design details around running workflows on top of actors.

## Planned features

You can see the list of features and their status using the [feature](https://github.com/cgillum/durabletask-dapr/issues?q=label%3Afeature) label in the issue tracker.

## Getting started

If you're interested in playing with this reference implementation, please see the README and the code under [test/DaprTesting](test/DaprTesting/).

### Prerequisites

The following dependencies must be installed on your machine to build and test this reference implementation.

* [.NET 6 SDK or newer](https://dotnet.microsoft.com/download/dotnet/6.0)
* [Dapr CLI 1.7 or newer](https://docs.dapr.io/getting-started/install-dapr-cli/)

## Contributing

Contributions via PRs are absolutely welcome! Keep in mind, however, that this is a reference implementation and not meant to be used for actual application development. A repo in the [Dapr GitHub organization](https://github.com/dapr) will be used for the real implementation.

Feel free to open issues in this repo to ask questions or provide feedback about the reference implementation. If you have questions or feedback about Dapr Workflow generally, please submit comments to https://github.com/dapr/dapr/issues/4576.
