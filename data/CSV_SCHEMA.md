# CSV Schema Documentation

## sample-events.csv
- `EventId` (int) - Primary key
- `EventName` (string) - Event name
- `EventDate` (date) - Event date
- `Description` (string) - Event Description
- `Location` (string) - Event Location

## sample-unit-locations.csv
- `ID` (guid) - Primary key
- `Name` (string) - Location name
- `Address Line1` (Address) - Location address line 1
- `Town` (string) - Location town
- `Postcode` (string) - Location postal code
- `What3Words` (string) - Location in what3words

## sample-units.csv
- `ID` (guid) - Primary key
- `Number` (int) - Unit number
- `Name` (string) - Location name
- `LocationID` (guid) - FK to sample-unit-locations.csv
- `Email` (string) - Contact email address
- `InstallationMonth` (string) - Month of unit installation meeting
- `MeetingSummary` (string) - Summary of unit meetings
- `WarrantIssued` (date) - Date the unit was issued a warrant
- `LastInstallationDate` (date) - Date of the last installation meeting
- `UnitType` (string) - Type of unit

## sample-officers.csv
- `ID` (guid) - Primary key
- `Order` (int) - Order they should be displayed
- `Abbreviation` (string) - Abbrevation of the name
- `Name` (string) - Officer name

## sample-unit-officers.csv
- `ID` (guid) - Primary key
- `UnitID` (guid) - FK to sample-units.csv
- `OfficerID` (guid) - FK to sample-officers.csv
- `LastName` (string) - Officer last name
- `Initials` (string) - Officer first name initials
- `Code` (string) - Reference code

## sample-unit-pmo.csv
- `ID` (guid) - Primary key
- `UnitID` (guid) - FK to sample-units.csv
- `LastName` (string) - Officer last name
- `Initials` (string) - Officer first name initials
- `Installed` (string) - Year the past master was installed
- `ProvRank` (string) - Current provincial rank
- `ProvRankIssued` (string) - Year the provincial rank was issued

## sample-unit-meetings.csv
- `ID` (guid) - Primary key
- `UnitID` (guid) - FK to sample-units.csv
- `Title` (string) - Title of the meeting
- `RecurrenceType` (string) - RecurrenceType
- `RecurrenceStrategy` (string) - RecurrenceStrategy
- `DayOfWeek` (string) - DayOfWeek
- `WeekNumber` (string) - WeekNumber
- `DayNumber` (string) - DayNumber
- `InstallationMonth` (string) - Month of installation meeting
- `StartMonth` (string) - StartMonth
- `EndMonth` (string) - EndMonth
- `Months` (string) - Colon seperated list of months
- `Override` (string) - Override