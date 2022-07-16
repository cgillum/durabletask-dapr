## Overview

This is a Node.js app that represents the notification service. It's invoked as a pub/sub subscriber and is triggered several times by the order processing workflow as it makes progress.

Note that the pub/sub configuration can be found in [pubsub.yaml](../components/pubsub.yaml).

## Prerequisites

* [Dapr CLI and initialized environment](https://docs.dapr.io/getting-started)
* [Latest Node.js installed](https://nodejs.org/)
* [ts-node](https://www.npmjs.com/package/ts-node)
* Make sure `npm` and `ts-node` are in the `PATH`

## Install dependencies

This will install TypeScript, express.js, and the Dapr SDK for JavaScript.

```bash
npm install
```

## Running the service

This will start up both the Dapr sidecar process and the Node.js app together.

```bash
npm run start:dapr
```
