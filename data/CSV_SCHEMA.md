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
- `Established` (date) - Date the unit was established
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
- `Code` (string) - Reference code

## sample-unit-pmi.csv
- `ID` (guid) - Primary key
- `UnitID` (guid) - FK to sample-units.csv
- `LastName` (string) - Masters last name
- `Initials` (string) - Masters first name initials
- `ProvRank` (string) - Current provincial rank
- `ProvRankIssued` (string) - Year the provincial rank was issued
- `Code` (string) - Reference code

## sample-unit-members.csv
- `ID` (guid) - Primary key
- `UnitID` (guid) - FK to sample-units.csv
- `LastName` (string) - Members last name
- `FirstNames` (string) - Members first names
- `Initials` (string) - Members first name initials
- `Joined` (string) - Year the member joined
- `ProvRank` (string) - Current provincial rank
- `Code` (string) - Reference code

## sample-unit-honrary.csv
- `ID` (guid) - Primary key
- `UnitID` (guid) - FK to sample-units.csv
- `LastName` (string) - Masters last name
- `Initials` (string) - Masters first name initials
- `GrandRank` (string) - Current grand rank
- `ProvRank` (string) - Current provincial rank
- `Code` (string) - Reference code

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