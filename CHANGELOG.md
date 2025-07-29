# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [7.1.6] - 2025-07-23
- Bump RDMP to v9.0.0

## [7.1.5] - 2025-06-10
- Bump RDMP to v8.4.5

## [7.1.4] - 2024-02-17

- Add DicomToCSV pipeline component
- Bump .Net8 to .Net9
- Bump RDMP from 8.4.2 to 8.4.3

## [7.1.3] 2024-12-02

- Bump RDMP from 8.3.1 to 8.4.0
- Bump HIC.BadMedicine.Dicom from 0.0.1 to 0.0.2
- Bump Microsoft.Extensions.Logging.Console from 8.0.1 to 9.0.0
- Bump Microsoft.NET.Test.Sdk from 17.11.1 to 17.12.0
- Bump fo-dicom from 5.1.4 to 5.1.5


## [7.1.2] 2024-10-28

- Bump fo-dicom from 5.1.3 to 5.1.4
- Bump HIC.DicomTypeTranslation from 4.1.3 to 4.1.5

## [7.1.1] 2024-10-16

- Update to target RDMP v8.3.1
- Bump Microsoft.Extensions.Logging.Console from 8.0.0 to 8.0.1
- Bump NunitXml.TextLogger tfrom 4..0.254 to 4.1.0

## [7.1.0] 2024-09-27

- Bump HIC.DicomTypeTranslation from 4.1.2 to 4.1.3
- Bump HIC.BadMedicine.Dicom from 0.1.0 to 0.1.1
- Bump NLog from 5.3.2 to 5.3.4
- Tidy attacher syntax/logic by @jas88 in https://github.com/SMI/RdmpDicom/pull/436

## [7.0.8] 2024-08-01

- Update to target RDMP v8.2.2

## [7.0.7] 2024-07-24

- Update to target RDMP version 8.2.1

## [7.0.6] 2024-07-08

- Updated to target RDMP version 8.2.0
- Update for changed RDMP LoadMetaData API

## [7.0.5] 2024-05-31

- Implement CodeMeaning/CodeValue extraction for ICD11 support
- Bump HIC.DicomTypeTranslation from 4.1.0 to 4.1.1

## [7.0.4] 2024-04-08

- Update RDMP API for new multi-load per catalogue

## [7.0.3] 2024-03-28

- Add ability to retry file fetch in FoDicomAnaonymiser
- Bump .net7 to .net8
- Fix issue with cross-platform file path mapping

## [7.0.2] 2024-02-14

- Fix issue with PutDicomFilesInExtractionDirectories option in FoDicom Pipeline Component

## [7.0.1]
 - Documentation Update
 - Update to target RDMP version 8.1.0
## [7.0.0] 2023-02-08

- Replace SharpCompress 0.32.2 with LibArchive.net 0.1.3 due to LZMA bugs
- Bump HIC.DicomTypeTranslation from 4.0.1 to 4.0.2
- Bump HIC.RDMP.Plugin from 8.0.5 to 8.0.7
- Bump Terminal.Gui from 1.8.2 to 1.9.0

## [6.0.2] 2022-11-04

- Documentation updates
- Updated to target RDMP version 8.0.5
- Update for changed CsvHelper API from RDMP

## [6.0.1] 2022-09-27

- Fixed issue running `ExecuteCommandCreateNewImagingDatasetSuite` when using a YamlRepository

## [6.0.0] 2022-09-27

- Updated to target RDMP version 8.0.0

## [5.0.12] 2022-09-09

- Fixed API for new `Bitmap` substitute (see https://github.com/HicServices/RDMP/pull/1355)

## [5.0.11] 2022-08-24

### Added

- Added MetadataOnly property to FoDicomAnonymiser [#64](https://github.com/SMI/RdmpDicom/issues/64)

## [5.0.10] 2022-06-21

- Refactored FoDicomAnonymiser to be easier for downstream consumption
- Added HTTP Basic Authentication support to SemEHR API endpoint connection.

## [5.0.9] 2022-05-27

- Added SemEHR Request Timeout property so that the timeout setting can be changed as needed for longer running queries.

## [5.0.8] 2022-05-04

### Changed
- Corrected resizing for the SemEHR API query builder - [#236](https://github.com/SMI/RdmpDicom/issues/236)

## [5.0.7] 2022-04-26

### Changed

- Bump SharpCompress from 0.30.1 to 0.31.0
- Bump HIC.RDMP.Plugin from 7.0.9 to 7.0.10

## [5.0.6] 2022-03-31

### Added

- Support for running CLI `AddTag` command for multiple Catalogues at once
- Extend repertoire of archive formats to include 7zip, RAR, tar, bz2 via SharpCompress
- New dependency: SharpCompress 0.30.1

### Changed

- Bump HIC.RDMP.Plugin from 7.0.7 to 7.0.9

## [5.0.5] 2022-03-03

- Bump HIC.RDMP.Plugin from 7.0.6 to 7.0.7

## [5.0.4] 2022-02-04

- Bump HIC.RDMP.Plugin from 7.0.5 to 7.0.6
- Added support for ignoring validation errors in SSL certificates

## [5.0.3] 2022-01-28

- Bump HIC.RDMP.Plugin from 7.0.3 to 7.0.5

### Added

- Added SkipAnonymisationOnStructuredReports option to FoDicomAnonymiser

## [5.0.2] 2021-11-15

- Added periodic disposal in ZipPool to prevent too many open file handles at once.

## [5.0.1] 2021-11-15

- Bump HIC.RDMP.Plugin from 7.0.1 to 7.0.3
- Added retry fields to `FoDicomAnonymiser` for when file system is unstable during extractions

## [5.0.0] 2021-11-03

- Added SemEHR cohort building prototype
- Bump HIC.RDMP.Plugin from 6.0.1 to 6.0.2

## [4.0.2] 2021-08-17

- Bump HIC.RDMP.Plugin from 6.0.0 to 6.0.1
- Added Verbose flag to PACSSource to cut down on logging

## [4.0.0] 2021-07-29

- Updated to work with RDMP 6.0 and FAnsi 2.0

## [3.0.1] 2021-07-14

### Changed

- Bump NunitXml.TestLogger from 3.0.97 to 3.0.107
- Bump HIC.RDMP.Plugin from 5.0.0 to 5.0.3

## Added

- Added support for customising `DicomClient` client settings in PACSSource caching component (e.g. `AssociationLingerTimeoutInMs`)
- Added logging of association requests in PACSSource
- Added new field MaximumAllowableAssociationEventsPerMinute for shutting down the executing process if the number of Association events crosses the given threshold

## [3.0.0] 2021-06-05

### Changed

- Updated to RDMP 5.0.0 (dotnet5) plugin layout and API

## Added

- FoDicomAnonymiser checks now support automatic database creation/patching for UID mapping server

## [2.2.4] 2021-03-08

### Changed

- When creating new imaging loads `UseAllTableInfoInLoadAsFieldMap` is enabled by default (improves load performance and stability)

## Fixed

- Fixed DLE not not loading files that are missing extensions when processing directory entries

## [2.2.3] 2021-03-05

### Changed

- Bump HIC.RDMP.Plugin from 4.2.1 to 4.2.3
- Bump HIC.DicomTypeTranslation from 2.3.1 to 2.3.2

## Fixed

- DLE now happily loads files on disk that are missing an extension e.g. USm123.213.432.234 (in the dicom standard the extension is optional)

## [2.2.2] 2021-01-19

## Fixed

- Fixed bug with `AutoRoutingAttacherWithPersistentRaw` data load module when used with RDMP 4.2.1 API


## [2.2.1] 2021-01-14

Updated to be compatible with RDMP 4.2

## [2.1.11] 2020-09-18

## Added

- Added TimeoutInSeconds property to `PrimaryKeyCollisionIsolationMutilation` DLE module

## [2.1.10] 2020-09-01

## Added

- Consecutively failing requests now result in delaying the fetch (incase server is busy with something)
- Added retry on failure/warning when fetching from PACS

## Fixed

- In PACSSource TransferTimeOutInSeconds now applies only to the current study being fetched (not the whole hour)
- Properly reported Warning and Cancel statuses in fetch request responses in PACSSource

## [2.1.9] 2020-08-28

- Refactor PACS fetch code with simpler queue handling
- Run integration test against RDMP 4.1.8 not 4.1.0 due to API changes
- Build plugin targetting .Net Standard 2.0 rather than deprecated Core 2.2

## [2.1.8] 2020-08-25

- Add local stub PACS in test package to enable further testing in future
- Point existing Orthanc unit test at public PACS server for better coverage
- Add unit tests to reproduce fixed issue below

### Fixed

- Handle duplicate object delivery better, to support retry scenario

### Added

- Added new command `CompareImagingSchemas` accessible from the Catalogue right click context menu.  The command shows differences between a live database table and the template used to create it.
- Added new command `AddTag` which adds a given dicom tag or typed column to the provided Catalogue (change is synced with RDMP and any `_Archive` tables)

## [2.1.7] 2020-08-17

### Fixed

- Allow the PACS to send us lossy compressed versions if it wants, otherwise we won't be able to receive anything it has in that format

### Added

- Accept video (MPEG/HEVC) content if the PACS offers it
- Added new cache source `ProcessBasedCacheSource` that calls out to a remote process

## [2.1.6] 2020-06-17

### Fixed

- Fixed logging exception during C-MOVE retries

## [2.1.5] 2020-06-03

### Added

- Single retry when ordering a C-MOVE fails

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

[Unreleased]: https://github.com/SMI/RdmpDicom/compare/v7.1.2...develop
[7.1.2]: https://github.com/SMI/RdmpDicom/compare/v7.1.1...v7.1.2
[7.1.1]: https://github.com/SMI/RdmpDicom/compare/v7.1.0...v7.1.1
[7.1.0]: https://github.com/SMI/RdmpDicom/compare/v7.0.8...v7.1.0
[7.0.8]: https://github.com/SMI/RdmpDicom/compare/v7.0.7...v7.0.8
[7.0.7]: https://github.com/SMI/RdmpDicom/compare/v7.0.6...v7.0.7
[7.0.6]: https://github.com/SMI/RdmpDicom/compare/v7.0.5...v7.0.6
[7.0.5]: https://github.com/SMI/RdmpDicom/compare/v7.0.4...v7.0.5
[7.0.4]: https://github.com/SMI/RdmpDicom/compare/v7.0.3...v7.0.4
[7.0.3]: https://github.com/SMI/RdmpDicom/compare/v7.0.2...v7.0.3
[7.0.2]: https://github.com/SMI/RdmpDicom/compare/v7.0.1...v7.0.2
[7.0.1]: https://github.com/SMI/RdmpDicom/compare/v7.0.0-rc1...v7.0.1
[7.0.0]: https://github.com/SMI/RdmpDicom/compare/v6.0.2...v7.0.0-rc1
[6.0.2]: https://github.com/SMI/RdmpDicom/compare/v6.0.1...v6.0.2
[6.0.1]: https://github.com/SMI/RdmpDicom/compare/v6.0.0...v6.0.1
[6.0.0]: https://github.com/SMI/RdmpDicom/compare/v5.0.12...v6.0.0
[5.0.12]: https://github.com/SMI/RdmpDicom/compare/v5.0.11...v5.0.12
[5.0.11]: https://github.com/SMI/RdmpDicom/compare/v5.0.10...v5.0.11
[5.0.10]: https://github.com/SMI/RdmpDicom/compare/v5.0.9...v5.0.10
[5.0.9]: https://github.com/SMI/RdmpDicom/compare/v5.0.8...v5.0.9
[5.0.8]: https://github.com/SMI/RdmpDicom/compare/v5.0.7...v5.0.8
[5.0.7]: https://github.com/SMI/RdmpDicom/compare/v5.0.6...v5.0.7
[5.0.6]: https://github.com/SMI/RdmpDicom/compare/v5.0.5...v5.0.6
[5.0.5]: https://github.com/SMI/RdmpDicom/compare/v5.0.4...v5.0.5
[5.0.4]: https://github.com/SMI/RdmpDicom/compare/v5.0.3...v5.0.4
[5.0.3]: https://github.com/SMI/RdmpDicom/compare/v5.0.2...v5.0.3
[5.0.2]: https://github.com/SMI/RdmpDicom/compare/v5.0.1...v5.0.2
[5.0.1]: https://github.com/SMI/RdmpDicom/compare/v5.0.0...v5.0.1
[5.0.0]: https://github.com/SMI/RdmpDicom/compare/v4.0.2...v5.0.0
[4.0.2]: https://github.com/SMI/RdmpDicom/compare/v4.0.0...v4.0.2
[4.0.0]: https://github.com/SMI/RdmpDicom/compare/v3.0.1...v4.0.0
[3.0.1]: https://github.com/SMI/RdmpDicom/compare/v3.0.0...v3.0.1
[3.0.0]: https://github.com/SMI/RdmpDicom/compare/v2.2.4...v3.0.0
[2.2.4]: https://github.com/SMI/RdmpDicom/compare/v2.2.3...v2.2.4
[2.2.3]: https://github.com/SMI/RdmpDicom/compare/v2.2.2...v2.2.3
[2.2.2]: https://github.com/SMI/RdmpDicom/compare/v2.2.1...v2.2.2
[2.2.1]: https://github.com/SMI/RdmpDicom/compare/v2.1.11...v2.2.1
[2.1.11]: https://github.com/SMI/RdmpDicom/compare/v2.1.10...v2.1.11
[2.1.10]: https://github.com/SMI/RdmpDicom/compare/v2.1.9...v2.1.10
[2.1.9]: https://github.com/SMI/RdmpDicom/compare/v2.1.8...v2.1.9
[2.1.8]: https://github.com/SMI/RdmpDicom/compare/v2.1.7...v2.1.8
[2.1.7]: https://github.com/SMI/RdmpDicom/compare/v2.1.6...v2.1.7
[2.1.6]: https://github.com/SMI/RdmpDicom/compare/v2.1.5...v2.1.6
[2.1.5]: https://github.com/SMI/RdmpDicom/compare/v2.1.4...v2.1.5
[2.1.4]: https://github.com/SMI/RdmpDicom/compare/v2.1.3...v2.1.4
[2.1.3]: https://github.com/SMI/RdmpDicom/compare/v2.1.2...v2.1.3
[2.1.2]: https://github.com/SMI/RdmpDicom/compare/v2.1.1...v2.1.2
[2.1.1]: https://github.com/SMI/RdmpDicom/compare/v2.1.0...v2.1.1
[2.1.0]: https://github.com/SMI/RdmpDicom/compare/v2.0.9...v2.1.0
[2.0.9]: https://github.com/SMI/RdmpDicom/compare/v2.0.8...v2.0.9
[2.0.8]: https://github.com/SMI/RdmpDicom/compare/v2.0.7...v2.0.8
[2.0.7]: https://github.com/SMI/RdmpDicom/compare/v2.0.6...v2.0.7
[2.0.6]: https://github.com/SMI/RdmpDicom/compare/v2.0.5...v2.0.6
[2.0.5]: https://github.com/SMI/RdmpDicom/compare/v2.0.4...v2.0.5
[2.0.4]: https://github.com/SMI/RdmpDicom/compare/v2.0.3...v2.0.4
[2.0.3]: https://github.com/SMI/RdmpDicom/compare/v2.0.2...v2.0.3
[2.0.2]: https://github.com/SMI/RdmpDicom/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/SMI/RdmpDicom/compare/v1.3.2...v2.0.1
[1.3.2]: https://github.com/SMI/RdmpDicom/compare/v1.3.1...v1.3.2
[1.3.1]: https://github.com/SMI/RdmpDicom/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/SMI/RdmpDicom/compare/v1.1.0...v1.3.0
[1.1.0]: https://github.com/SMI/RdmpDicom/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/SMI/RdmpDicom/compare/1581c5ae3a12db1873f4cf1a930215750ad2ae14...v1.0.2
[Tag Elevation]:https://github.com/SMI/DicomTypeTranslation/tree/main/DicomTypeTranslation/Elevation
