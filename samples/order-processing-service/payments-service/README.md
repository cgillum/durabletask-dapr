## Overview

This is a Node.js app that represents the payments service. It's called by the order processing workflow when completing the order process. The workflow uses the Dapr service invocation building block to invoke this service.

## Prerequisites

* [Dapr CLI and initialized environment](https://docs.dapr.io/getting-started)
* [Latest Node.js installed](https://nodejs.org/)
* [ts-node](https://www.npmjs.com/package/ts-node)
* Make sure `npm` and `ts-node` are in the `PATH`

## Install dependencies

This will install TypeScript and express.js.

```bash
npm install
```

## Running the service

This will start up both the Dapr sidecar process and the Node.js app together.

```bash
npm run start:dapr
```
