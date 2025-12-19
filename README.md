# Remittance Advice Manager

A modern WPF desktop application for managing remittance advice PDFs with integrated WebDB upload and reporting capabilities.

## Features

### 1. Downloaded Files Tab
- Display all downloaded remittance advice PDF files
- Track file status, download dates, and advice numbers
- Print individual files or batch print multiple files
- Mark files as printed
- Delete files from tracking database

### 2. Altered PDFs Tab
- View and manage processed/altered PDF files
- Same printing and tracking capabilities as Downloaded Files
- Track transformation history

### 3. Upload to WebDB Tab
- Authenticate with WebDB using ASP.NET Forms Authentication
- Batch upload remittance advice files to WebDB
- Progress tracking for uploads
- View upload results with success/error messages
- Select individual files or batch select all
- Option to overwrite existing files

### 4. Download Reports Tab
- Download Provider Check Totals report from WebDB
- Select advice date and vendor number parameters
- Download reports in PDF format
- Print reports directly from the application
- Save reports to disk

## Technology Stack

- **.NET 8.0 (Windows)**
- **WPF** with MVVM pattern using CommunityToolkit.Mvvm
- **Dependency Injection** via Microsoft.Extensions.DependencyInjection
- **Entity Framework Core** with SQLite for local data tracking
- **iText7** for PDF processing
- **Selenium WebDriver** for browser automation (future use)
- **AngleSharp** for HTML parsing (ASP.NET ViewState extraction)
- **Serilog** for logging

## Architecture

The application follows clean architecture principles with clear separation of concerns:

- **Models**: Data models (RemittanceFile, UploadResult, ReportParameters, WebDbCredentials)
- **ViewModels**: MVVM ViewModels using CommunityToolkit.Mvvm source generators
- **Views**: WPF UserControls with XAML UI
- **Services**: Business logic abstracted behind interfaces
  - FileTrackingService: SQLite database operations
  - PdfProcessingService: PDF manipulation with iText7
  - WebDbAuthenticationService: Forms authentication with ViewState parsing
  - RemittanceUploadService: Multipart file upload to WebDB
  - ReportDownloadService: Report retrieval from WebDB
  - PrintService: PDF printing capabilities

## Configuration

Edit `appsettings.json` to configure:

```json
{
  "WebDb": {
    "BaseUrl": "https://localhost:44356",
    "LoginPath": "/Login.aspx",
    "UploadPath": "/Remittances/UploadAdvice.aspx",
    "ReportPath": "/Reports/Internal/ProviderCheckTotals.aspx",
    "TimeoutSeconds": 300
  },
  "Storage": {
    "DownloadFolder": "C:\\RemittanceAdvice\\Downloaded",
    "AlteredFolder": "C:\\RemittanceAdvice\\Altered",
    "DatabaseFile": "remittance_tracker.db"
  }
}
```

## WebDB Integration

The application integrates with an ASP.NET Web Forms application (WebDB) using:

- **Forms Authentication**: Parses __VIEWSTATE, __VIEWSTATEGENERATOR, and __EVENTVALIDATION using AngleSharp
- **Cookie Management**: Maintains .ASPXAUTH cookie across requests
- **Multipart File Upload**: Sends PDF files with proper form data
- **Report Download**: Retrieves reports via query string parameters

### Authentication
Users must authenticate with valid WebDB credentials. The Upload and Download Reports features require an authenticated session.

## Database

The application uses SQLite to track remittance files locally with the following schema:

```sql
CREATE TABLE RemittanceFiles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FileName TEXT NOT NULL,
    FilePath TEXT NOT NULL,
    DownloadedDate DATETIME NOT NULL,
    Status TEXT NOT NULL,  -- Downloaded, Altered, Uploaded, Error
    AdviceNumber TEXT,
    IsPrinted INTEGER DEFAULT 0,
    UploadedDate DATETIME,
    ErrorMessage TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

## Building and Running

### Prerequisites
- .NET 8.0 SDK
- Windows OS (for WPF)

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run --project src/RemittanceAdviceManager
```

Or open the solution in Visual Studio and run from there.

## Logging

Logs are written to:
- Console (during development)
- `logs/app-{Date}.log` files (rolling daily)

Log levels can be configured in `appsettings.json`.

## Future Enhancements

- Implement actual advice downloader functionality using Selenium WebDriver
- Add PDF preview capabilities using embedded PDF viewer
- Implement credential encryption for "Remember Me" feature using Windows DPAPI
- Add more report types beyond Provider Check Totals
- Implement automated download scheduling
- Add export capabilities (Excel, CSV)
- Enhance error handling and retry logic
- Add unit tests and integration tests

## License

This project is proprietary software for internal use.

## Author

Created for Home Care Montana
