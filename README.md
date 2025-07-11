# Konqvist

Konqvist is a web application built with Blazor and .NET 9. It serves as a modern platform for [briefly describe the main purpose or domain of the project, e.g., managing tasks, tracking inventory, etc.].

## Project Structure
- **Konqvist.Web**: The main Blazor web application.
- **Konqvist.Data**: Data access and business logic.
- **Konqvist.Data.Tests**: Unit tests for the data layer.

## Getting Started (Development Setup)

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- A modern IDE (e.g., Visual Studio 2022+, VS Code)

### Setup Steps

1. **Clone the repository** 

   ```
   git clone <repository-url> cd Konqvist/src
   ```

2. **Install SSL Development Certificate** 
   
   Start a new console as an Administrator. Change into the root directory of the
   project, run the following script to set up a local SSL certificate.

   ```
   > ./setup-local-ssl.cmd
   ```
 
3. **Restore dependencies**
   
   ```
   > dotnet restore
   ```

4. **Build the solution**

   ```
   > dotnet build
   ```

5. **Run the application**

   ```
   > dotnet run --project Konqvist.Web/Konqvist.Web.csproj
   ```


### Running Tests

   To run unit tests use the following command.

   ```
   > dotnet test
   ```

## Additional Notes
- The application uses Blazor Serverfor the frontend and targets .NET 9.
- For local development, ensure SSL certificates are trusted.


