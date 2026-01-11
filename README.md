# Activity Gear Sync

[![Build](https://github.com/stephanprobst/ActivityGearSync/actions/workflows/build.yml/badge.svg)](https://github.com/stephanprobst/ActivityGearSync/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

A cross-platform .NET console application for bulk editing gear assignments on your Strava activities.

## Features

- Bulk update gear assignments on multiple activities at once
- Bulk update activity types (e.g., change Ride to GravelRide)
- Bulk update activity flags (commute, trainer, privacy)
- Bulk edit activity names and descriptions
- Export activities to GPX/TCX files for backup or transfer (Beta)
- Filter activities by type, date range, and current values
- View your activities and gear
- Built-in self-update from GitHub releases
- Secure local storage of API credentials (AES encrypted)
- Rate limiting to respect API quotas

## Installation

### Download and Run

1. Go to [GitHub Releases](https://github.com/stephanprobst/ActivityGearSync/releases)
2. Download the file for your platform:
   - **Windows**: `ActivityGearSync-win-x64-x.x.x.exe`
   - **Linux**: `ActivityGearSync-linux-x64-x.x.x`
   - **macOS**: `ActivityGearSync-osx-arm64-x.x.x`
3. Run the application

### First-Time Setup

On first run, the application will guide you through:

1. Creating a Strava API application at https://www.strava.com/settings/api
2. Uploading an application icon (auto-generated for you)
3. Entering your Client ID and Client Secret
4. Authenticating with your Strava account

## Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build and Run

```bash
git clone https://github.com/stephanprobst/ActivityGearSync.git
cd ActivityGearSync/ActivityGearSync
dotnet build
dotnet run
```

## Usage

The main menu provides the following options:

- **Update Gear on Activities** - Bulk assign or remove gear from multiple activities
- **Update Activity Type** - Bulk change sport types within the same category
- **Update Activity Flags** - Bulk update commute, trainer, or privacy flags
- **Update Activity Name/Description** - Bulk edit names and descriptions
- **Export Activities (GPX/TCX)** - Export activities to GPX or TCX files (Beta)
- **View My Activities** - Browse your recent activities
- **View My Gear** - See your configured bikes and shoes
- **Check for Updates** - Check for and install new versions from GitHub

## Security

- API credentials are stored locally in your user profile directory
- All sensitive data is encrypted using AES-256
- No data is sent to any third party

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

This application is not affiliated with, endorsed by, or connected to Strava, Inc. It uses the Strava API in accordance with the [Strava API Agreement](https://www.strava.com/legal/api).
