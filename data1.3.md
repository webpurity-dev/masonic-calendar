# Data version 1.3
We now have finalised data format consisting of two files:
- E:\Development\repos\masonic-calendar\document\data\units_v1.3.csv
- E:\Development\repos\masonic-calendar\document\data\membership_v1.3.csv

## Units 1.3
This contains all the units we need to render data for and has been extended to include the supplementary information about each unit.  This is a single file for all units with a "Unit Type" column to seperate the uniets per type.

The types are:
- Craft
- RA (Royal Arch)
- Mark
- RAM (Royal Ark Mariners)

We need to update the data sources YAML to filter to the units by "unit type" and then filter to the members by "unit type" and "unit no".

We need to change the logic to iterate each unit in the units_v1.3.csv and from here pull out the relevant officers, members, past masters, etc.

## Membership 1.3
This contains all the members for all the units and has been cleaned to ensure the names are all in a consistent format, ranks and dates cleaned, and a unique reference for each row.  There is a new "PosNo" columns which defines the order for the tables within each unit, and a "OffPos" column whic defines the order for the officers table.

I am proposing to change the data source YAML files, so the craft_data_sources.yaml would be like this:

```
units:
  source: "units_v1.3.csv"
  filter_field: "Unit Type"
  filter_value: "Craft
  fields:    
    - name: "Number"
      csv_column: "Unit No"
      type: "int"
    - name: "Name"
      csv_column: "Unit Name"
      type: "string"
    - name: "ShortName"
      csv_column: "ShortName"
      type: "string"
    - name: "Warrant"
      csv_column: "Warrant"
      type: "string"
    - name: "MeetingDates"
      csv_column: "Meeting Dates"
      type: "string"
    - name: "LastInstallationDate"
      csv_column: "Last Installation"
      type: "string"
    - name: "Location"
      csv_column: "Location"
      type: "string"
    - name: "Email"
      csv_column: "Email"
      type: "string"

officers:
  source: "membership_v1.3.csv"
  filters:
    - filter_field: "Unit Type"
      filter_value: "Craft
    - filter_field: "MemType"
      filter_value: "Off"
  fields:
    - name: "Reference"
      csv_column: "UniqueRef"
      type: "string"
    - name: "Name"
      csv_column: "Name"
      type: "string"
    - name: "Position"
      csv_column: "Office"
      type: "string"
    - name: "PositionNo"
      csv_column: "OffPos"
      type: "int"

past_masters:
  source: "membership_v1.3.csv"
  filters:
    - filter_field: "Unit Type"
      filter_value: "Craft
    - filter_field: "MemType"
      filter_value: "PMO"
  fields:
    - name: "Reference"
      csv_column: "UniqueRef"
      type: "string"
    - name: "Name"
      csv_column: "Name"
      type: "string"
    - name: "YearInstalled"
      csv_column: "Installed"
      type: "string"
    - name: "ProvincialRank"
      csv_column: "Provincial Rank"
      type: "string"
    - name: "GrandRank"
      csv_column: "Grand Rank"
      type: "string"
    - name: "RankYear"
      csv_column: "Date Accorded"
      type: "string"
    - name: "ActiveProvincialRank"
      csv_column: "Active Provincial Rank"
      type: "string"
    - name: "ActiveRankYear"
      csv_column: "Active Accorded"
      type: "string"

joining_past_masters:
  source: "membership_v1.3.csv"
  filters:
    - filter_field: "Unit Type"
      filter_value: "Craft
    - filter_field: "MemType"
      filter_value: "PMI"
  fields:
    - name: "Reference"
      csv_column: "UniqueRef"
      type: "string"
    - name: "Name"
      csv_column: "Name"
      type: "string"
    - name: "PastUnits"
      csv_column: "Join Unit"
      type: "string"
    - name: "GrandRank"
      csv_column: "Grand Rank"
      type: "string"
    - name: "ProvincialRank"
      csv_column: "Provincial Rank"
      type: "string"
    - name: "RankYear"
      csv_column: "Date Accorded"
      type: "string"

members:
  source: "membership_v1.3.csv"
  filters:
    - filter_field: "Unit Type"
      filter_value: "Craft
    - filter_field: "MemType"
      filter_value: "Mem"
  fields:
    - name: "Reference"
      csv_column: "UniqueRef"
      type: "string"
    - name: "Name"
      csv_column: "Name"
      type: "string"
    - name: "YearInitiated"
      csv_column: "Year"
      type: "string"

honorary_members:
  source: "membership_v1.3.csv"
  filters:
    - filter_field: "Unit Type"
      filter_value: "Craft
    - filter_field: "MemType"
      filter_value: "Hon"
  fields:"
      csv_column: "UniqueRef"
      type: "string"
    - name: "Title"
      csv_column: "Hon Rank"
      type: "string"
    - name: "Name"
      csv_column: "Name"
      type: "string"
    - name: "GrandRank"
      csv_column: "Grand Rank"
      type: "string"
    - name: "ProvincialRank"
      csv_column: "Provincial Rank"
      type: "string"
```