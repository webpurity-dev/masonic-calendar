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
- `WarrantIssued` (date) - Date the unit warrant was issues