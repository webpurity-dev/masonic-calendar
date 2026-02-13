# hermes-export.csv
Single extract of all unit data split into sections based on 'type'.
- `Unit` (int) - Unit number.  FK to 'Number' in sample-units.csv
- `Type` (string) - reference to section/table witin a unit page. 
    - Off=Officers,PMO=Past Masters,PMI=Joining Past Masters,Mem=Members,Hon=Honorary members

## hermes-export.csv (officers) where Type=Off
- `PosNo` (int) - Order of the entry within it's associated table
- `Name` (string) - Full name of officer including initials
- `FN01` (string) - Position of the officer

## hermes-export.csv (past masters) where Type=PMO
- `PosNo` (int) - Order of the entry within it's associated table
- `Name` (string) - Full name of officer including initials
- `FN01` (string) - Year the officer was installed
- `FN13` (string) - Current provincial rank
- `FN14` (string) - Year the provincial rank was issued

## hermes-export.csv (joining past masters) where Type=PMI
- `PosNo` (int) - Order of the entry within it's associated table
- `Name` (string) - Full name of officer including initials
- `FN01` (string) - Year the officer was installed
- `FN12` (string) - Current provincial rank
- `FN13` (string) - Year the provincial rank was issued

## hermes-export.csv (members) where Type=Mem
- `PosNo` (int) - Order of the entry within it's associated table
- `Name` (string) - Full name of officer including initials
- `FN01` (string) - Year the member was initiated

## sample-units.csv
- `ID` (guid) - Primary key
- `Number` (int) - Unit number
- `Name` (string) - Location name
- `LocationID` (guid) - FK to sample-unit-locations.csv
- `Email` (string) - Contact email address
- `Established` (date) - Date the unit was established
- `LastInstallationDate` (date) - Date of the last installation meeting
- `UnitType` (string) - Type of unit