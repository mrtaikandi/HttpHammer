# HttpHammer

[![NuGet](https://img.shields.io/nuget/v/HttpHammer.svg)](https://www.nuget.org/packages/HttpHammer/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A command line tool for load testing and benchmarking HTTP APIs. HttpHammer allows you to define requests in a YAML configuration file and execute them with configurable concurrency and volume.

## Features

- Define API requests in a simple YAML configuration format
- Support for warmup requests to prepare your test environment
- Concurrent request execution with configurable parallelism
- Variable substitution to dynamically modify requests
- Extract values from responses and use them in subsequent requests
- Detailed performance metrics and reporting
- Support for multiple HTTP methods (GET, POST, PUT, etc.)
- Header and body customization for requests
- Interactive mode with progress tracking
- Support for delays and user prompts in test plans

## Installation

HttpHammer is distributed as a .NET Tool. You can install it globally using the .NET CLI:

```bash
dotnet tool install -g HttpHammer
```

Or update to the latest version:

```bash
dotnet tool update -g HttpHammer
```

## Requirements

- .NET 9.0 or later

## Usage

```bash
httphammer --file plan.yaml
```

If you don't specify a file, you will be prompted to enter the path to your YAML configuration file.

### Command Line Options

- `--file`, `-f` - Path to the plan YAML configuration file
- `--debug`, `-d` - Enable debug logging to the console
- `--verbose`, `-v` - Enable verbose logging to the console (requires --debug)

## Configuration Format

HttpHammer uses YAML files to define the execution plan. Here's a basic example:

```yaml
variables:
  baseUrl: http://localhost:5200
  apiVersion: 1.0

warmup:
  - request:
      name: Authenticate
      description: Request token with password-grant
      method: POST
      url: ${baseUrl}/auth
      headers:
        Content-Type: application/x-www-form-urlencoded
      body: "grant_type=password&username=admin&password=admin"
      response:
        status_code: 200
        content:
          access_token: =>{access_token}

requests:
  - name: Get Data
    description: Fetch data from the API
    concurrent_connections: 10
    max_requests: 100
    method: GET
    url: ${baseUrl}/api/data
    headers:
      Authorization: Bearer ${access_token}
```

### Configuration Sections

#### Variables

Global variables that can be used throughout the configuration file.

```yaml
variables:
  baseUrl: http://localhost:5200
  apiKey: your-api-key
```

The application also dynamically adds extracted variables from the warmup requests and the following variables during execution.


| Field      | Description                                                  |
|------------|--------------------------------------------------------------|
| `request`  | The current request number.                                  |
| `timestamp`| The timestamp when the test started in unix timestamp format.|

#### Warmup

Requests that are executed **sequentially** before the main test starts. Useful for authentication, setting up test data or simply warming up the API server.

```yaml
warmup:
  - request: # Defines an HTTP request
      name: # The request display name
      description: # Description of what the request does
      method: # HTTP method (GET, POST, etc.)
      url: # URL for the request, can use variables like ${baseUrl}
      max_requests: # Maximum number of requests to send (default: 1)
      headers:
        Header-Name: # Header value
      body: # Request body (string or JSON)
      response:
        status_code: # Expected status code (default: 200)
        content:
          json_field: =>{variable_name}  # Extract json_filed value and store in variable_name
        headers:
          header-name: =>{variable_name}  # Extract header-name value and store in variable_name

  - delay: # Pause execution for a specified duration
      name: # The delay display name
      duration: # Duration in milliseconds

  - prompt: # Request user input during the test
        name: # The prompt display name
        message: # Message to display to the user
        allow_empty: # Whether to allow empty input (default: false)
        secret: # Whether to hide input like a password (default: false)
        variable: # Variable name to store the input
        default: # Default value to display in the prompt
```

#### Requests

The main requests that will be executed concurrently.

```yaml
requests:
  - name: # The request display name
    description: # Description of what the request does
    method: # HTTP method (GET, POST, etc.)
    url: # URL for the request, can use variables like ${baseUrl}
    max_requests: # Maximum number of requests to send (default: 1)
    headers:
      Header-Name: # Header value
    body: # Request body (string or JSON)
```

#### Special Processors

HttpHammer supports special processors for more complex test scenarios. These processors can be used in the `warmup` section to perform actions like delays, user prompts, and more.

##### Delay Processor

Pause execution for a specified duration:

```yaml
warmup:
  - delay: # Pause execution for a specified duration
    name: # The delay display name
    duration: # Duration in milliseconds
```

##### Prompt Processor

Request user input during the test:

```yaml
warmup:
  - prompt:
      name: # The prompt display name
      message: # Message to display to the user
      allow_empty: # Whether to allow empty input (default: false)
      secret: # Whether to hide input like a password (default: false)
      variable: # Variable name to store the input
      default: # Default value to display in the prompt
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.