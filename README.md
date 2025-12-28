# Activity Gear Sync

A cross-platform .NET console application for bulk editing gear assignments on your Strava activities.

## Features

- Bulk update gear assignments on multiple activities at once
- Filter activities by type, date range, and current gear
- View your activities and gear
- Secure local storage of API credentials (AES encrypted)
- Rate limiting to respect API quotas

## Requirements to build the project

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- A Strava account
- Your own Strava API application credentials

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/stephanprobst/ActivityGearSync.git
cd ActivityGearSync
```

### 2. Build and run

```bash
cd ActivityGearSync
dotnet run
```

### 3. First-time setup

On first run, the application will guide you through:

1. Creating a Strava API application at https://www.strava.com/settings/api
2. Uploading an application icon (auto-generated for you)
3. Entering your Client ID and Client Secret

### 4. Authenticate

After setup, authenticate with your account to grant the application access to your activities and gear.

## Usage

The main menu provides the following options:

- **Update Gear on Activities** - Bulk assign or remove gear from multiple activities
- **View My Activities** - Browse your recent activities
- **View My Gear** - See your configured bikes and shoes

## Security

- API credentials are stored locally in your user profile directory
- All sensitive data is encrypted using AES-256
- No data is sent to any third party

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

This application is not affiliated with, endorsed by, or connected to Strava, Inc. It uses the Strava API in accordance with the [Strava API Agreement](https://www.strava.com/legal/api).
