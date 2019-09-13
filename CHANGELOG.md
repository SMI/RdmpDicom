# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

...

## [1.3.0] 2019-09-13

## Changed

- Changed default source for load pipeline to files (previously JSON)
- Updated to RDMP API 3.2

## Removed

- Removed 'Create With Default' button from image table creation user interface (All imaging table creation is now done through templates, there is no default).

## [1.1.0] - 2019-07-31

### Added

- Dicom reading pipeline components can now store [Tag Elevation] configuration using property TagElevationConfigurationXml (in the database).  Previously the only option was to use an external file referenced by TagElevationConfigurationFile (this option is still available)

### Changed

- Updated to latest RDMP API (3.1.0)

## [1.0.2] - 2019-07-08

### Added 

- Initial commit from private repo


[Unreleased]: https://github.com/HicServices/RdmpDicom/compare/v1.3.0...develop
[1.3.0]: https://github.com/HicServices/RdmpDicom/compare/v1.1.0...v1.3.0
[1.1.0]: https://github.com/HicServices/RdmpDicom/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/HicServices/RdmpDicom/compare/1581c5ae3a12db1873f4cf1a930215750ad2ae14...v1.0.2
[Tag Elevation]:https://github.com/HicServices/DicomTypeTranslation/tree/develop/DicomTypeTranslation/Elevation
