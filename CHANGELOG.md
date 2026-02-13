# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.2] - 2026-01-05

### Fixed
- *clears throat*... actually fixed pathing issue in deployment.

## [2.1.1] - 2026-01-05

### Fixed
- Fixed deployment path issue (and changed build process to avoid this in future).

## [2.1.0] - 2026-01-05

### Added
- Added full controller support for the voting menu, including navigation with left stick, confirmation with A button, and subtle visual focus indicators.
- Improved menu styling with refined hover and selection effects for better usability.

### Changed
- Enhanced input handling to detect and respond to controller vs. mouse/keyboard input schemes.

## [2.0.0] - 2026-01-05

### Added
- Added art to source.
- Loaded custom terminal texture.
- Added sign overlay for viewing upcoming destinations.

### Changed
- Replaced HUD UI and keybinds with kiosk-based menu.
- Generalized debug placement and tuned kiosk position.
- Updated image with votes to show destination.
- Simplified controls: interact with the kiosk for a brand new menu. No more HUD elements; all details are in the menu and starting to take shape within the airport. Look out for a sign to see your upcoming destinations!
- Improved debug logic for dumping airport game objects.

### Fixed
- Adjusted the size of the added kiosk.

## [1.0.0] - 2026-01-02

### Added
- Art and real README.
- Clear myvote indicators and indicate winners.
- Display destinations and votes.
- Networking and debug output.
- Numpad biome forcing.
- Initial project setup.

### Changed
- Renamed to VacationPlanner.
- Simplified implementation.
