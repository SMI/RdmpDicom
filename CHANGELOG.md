# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- Fixed issue with failed store requests hanging ordering

## [2.1.4] 2020-05-26

- Bugfix: respond correctly to far-end association release requests in CachingSCP
- Revert 2.1.3's temporary workaround for the above issue

## [2.1.3] 2020-05-21

### Added

- Add condition variable for CachingSCP to alert PACSSource when transfer completes
- Added version number logging of plugin and fo-dicom

## [2.1.2] 2020-05-18

### Changed

- Updated Networking API for PACSSource to latest (non deprecated) `DicomClient`

## [2.1.1] 2020-05-11

### Fixed

- Reduced the number of instances of `DicomClient` being used in PACSSource

## [2.1.0] 2020-05-05

### Added

- Added PostgresSql support in PrimaryKeyCollisionIsolationMutilation

### Changed

- Updated to RDMP 4.1.0
- Updated to DicomTypeTranslation 2.2.0

## [2.0.9] 2020-04-15

### Added

- Added tool to help visualize primary key collisions stored in an isolation table.

## [2.0.8] 2020-03-27

- Bump HIC.DicomTypeTranslation from `2.1.2` to `2.2.0`
- This includes an update to fo-dicom from `4.0.1` to `4.0.4`

## [2.0.7] 2020-02-05

## Added

- Support for zip file notation when loading dicoms from zip files (e.g. load only `c:\myzip!1.dcm` and `c:\myzip!3.dcm` )
- Upgraded to latest RDMP release (4.0.2)
...

## [2.0.6] 2020-01-06

### Changed

- `PrimaryKeyCollisionIsolationMutilation` now supports non string primary/foreign values.

### Fixed

- Fixed path in error messages from `FoDicomAnonymiser` (e.g. when failing to anonymize an image) to show full path
- Fixed bug in `PrimaryKeyCollisionIsolationMutilation` when collisions occur in multiple sets of child records of a primary key.
    - All records are now migrated at once then deleted at once
    - See test [Test_IsolateTwoTables_MultipleCollidingChildren](./Rdmp.Dicom.Tests/Unit/PrimaryKeyCollisionIsolationMutilationTests.cs)
- Fixed bug in `PrimaryKeyCollisionIsolationMutilation` when child tables have collisions involving different parent foreign key references
    - All foreign key values are read from colliding records
    - See test [Test_IsolateTables_AmbiguousFk](./Rdmp.Dicom.Tests/Unit/PrimaryKeyCollisionIsolationMutilationTests.cs)
    
## [2.0.5] 2019-12-12

### Added

- Added better logging of error(s) in `FoDicomAnonymiser` (now includes file path of image failing)

### Changed

- DicomSource now expresses relative paths (where possible) with the `./` prefix e.g. `./subdir/1.dcm` (previously `subdir/1.dcm`)

### Fixed

- Fixed relative path expression bug when loading an explicit file list (of dicoms).  Bug caused file path to be expressed as filename only (i.e. no path)
- Fixed DicomSource not expressing subdirectories of zip files (meaning it previously only worked when everything was in the root of the zip file).

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

[Unreleased]: https://github.com/HicServices/RdmpDicom/compare/v2.1.4...develop
[2.1.4]: https://github.com/HicServices/RdmpDicom/compare/v2.1.3...v2.1.4
[2.1.3]: https://github.com/HicServices/RdmpDicom/compare/v2.1.2...v2.1.3
[2.1.2]: https://github.com/HicServices/RdmpDicom/compare/v2.1.1...v2.1.2
[2.1.1]: https://github.com/HicServices/RdmpDicom/compare/v2.1.0...v2.1.1
[2.1.0]: https://github.com/HicServices/RdmpDicom/compare/v2.0.9...v2.1.0
[2.0.9]: https://github.com/HicServices/RdmpDicom/compare/v2.0.8...v2.0.9
[2.0.8]: https://github.com/HicServices/RdmpDicom/compare/v2.0.7...v2.0.8
[2.0.7]: https://github.com/HicServices/RdmpDicom/compare/v2.0.6...v2.0.7
[2.0.6]: https://github.com/HicServices/RdmpDicom/compare/v2.0.5...v2.0.6
[2.0.5]: https://github.com/HicServices/RdmpDicom/compare/v2.0.4...v2.0.5
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
