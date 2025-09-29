# Are You The One? Information Theory Analysis

A .NET 8.0 console application that analyzes the reality TV show "Are You The One?" using information theory principles to simulate and calculate optimal matching strategies.

## Overview

This project simulates the matching process from the reality TV show "Are You The One?" and calculates the information gained from different matching scenarios. It uses information theory to determine the most efficient strategies for identifying perfect matches.

## Features

- **Information Theory Analysis**: Calculates information gained from different matching outcomes
- **Match Simulation**: Simulates multiple rounds of matching scenarios
- **Score Change Tracking**: Analyzes how scores change between different match configurations
- **Statistical Analysis**: Provides average information scores across multiple iterations

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Visual Studio, Visual Studio Code, or JetBrains Rider (optional)

### Building and Running

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd AreYouTheOne.Console
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the solution:
   ```bash
   dotnet build
   ```

4. Run the console application:
   ```bash
   dotnet run --project AreYouTheOne.Console
   ```

## Project Structure

- **AreYouTheOne.Console**: Main console application that runs the simulation
- **AreYouTheOne**: Core library containing the game logic and entities
- **Data/**: Contains data processing and management logic
- **Helpers/**: Utility functions and helper methods

## How It Works

The application runs 1000 iterations of match simulations, where each iteration:

1. Generates a new matching scenario using manual information theory
2. Calculates score changes between consecutive match-ups
3. Computes information gained using logarithmic probability calculations
4. Tracks running averages and final statistics

## Configuration

You can adjust the logging level in `Program.cs` by modifying the `SetMinimumLevel` parameter:

```csharp
builder.SetMinimumLevel(LogLevel.Critical); // Change this to set the minimum log level
```

Available log levels: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

This project is for educational and entertainment purposes.