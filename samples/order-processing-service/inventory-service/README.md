## Overview

This is a Python Flask app that represents an inventory service. It's called by the order processing workflow when checking to see if there is sufficient inventory for a particular order. The workflow uses the Dapr service invocation building block to invoke this service.

## Prerequisites

* [Dapr CLI and initialized environment](https://docs.dapr.io/getting-started)
* [Python 3.7+ installed](https://www.python.org/downloads/)
* `PATH` environment variable includes `python` command and maps to Python 3.

## Install dependencies

This will install Flask as well as the Dapr SDK for Python.

```bash
pip3 install -r requirements.txt 
```

## Running the service

This will start up both the Dapr sidecar process and the Python app together.

```bash
dapr run --app-id inventory --app-port 5006 --dapr-http-port 3506 -- python app.py
```
