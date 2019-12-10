# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

...

### Fixed

- Fixed relative path expression bug when loading an explicit file list (of dicoms).  Bug caused file path to be expressed as filename only (i.e. no path)

## [2.0.4] 2019-12-04

### Changed

- Upgraded to latest RDMP API (4.0.1)

## [2.0.3] 2019-11-21

### Changed

- Updated to latest DicomTypeTranslation package
- Upgraded to latest RDMP package

## [2.0.2] 2019-11-13

### Fixed

- Fixed crash when setting `ArchiveRoot` to null on a `DicomSource`

## [2.0.1] 2019-11-12

### Added

- Added support for Linux style paths e.g. archive `"/archive/root"` with subdir `"series1/1.dcm"`

### Changed

- Updated to RDMP 4.0.1-rc1
- Local paths are now expressed without a leading "/" (e.g. `series1/1.dcm` when previously it would be `/series1/1.dcm`)
- Database paths now use `/` instead of `\` to work with both Windows and Linux

## Fixed

- Fixed bug in FoDicomAnonymiser when using a UID mapping repository with sql authentication (username/password)
- Fixed ZipPool not working in case sensitive file systems (e.g. Linux)

## [1.3.2] 2019-10-30

## Changed

- Updated to RDMP 3.2.1

## [1.3.1] 2019-10-18

### Added

- Added linux x64 binaries to enable plugin to work from CLI engines running in linux hosts (e.g. data load engine)

## Removed

- Removed dlls that are already part of RDMP core application (e.g. Rdmp.Core.dll) from plugin archive

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

[Unreleased]: https://github.com/HicServices/RdmpDicom/compare/v2.0.4...develop
[2.0.4]: https://github.com/HicServices/RdmpDicom/compare/v2.0.3...v2.0.4
[2.0.3]: https://github.com/HicServices/RdmpDicom/compare/v2.0.2...v2.0.3
[2.0.2]: https://github.com/HicServices/RdmpDicom/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/HicServices/RdmpDicom/compare/v1.3.2...v2.0.1
[1.3.2]: https://github.com/HicServices/RdmpDicom/compare/v1.3.1...v1.3.2
[1.3.1]: https://github.com/HicServices/RdmpDicom/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/HicServices/RdmpDicom/compare/v1.1.0...v1.3.0
[1.1.0]: https://github.com/HicServices/RdmpDicom/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/HicServices/RdmpDicom/compare/1581c5ae3a12db1873f4cf1a930215750ad2ae14...v1.0.2
[Tag Elevation]:https://github.com/HicServices/DicomTypeTranslation/tree/develop/DicomTypeTranslation/Elevation
