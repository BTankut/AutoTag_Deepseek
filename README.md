# AutoTag_Deepseek - Revit Tag Management Plugin

A Revit plugin that automatically organizes and positions tags.

## Features

- Automatic tag arrangement along Y-axis
- Smart positioning based on tag list direction:
  - +X direction: Top to bottom normal ordering
  - -X direction: Top to bottom reverse ordering (to prevent leader conflicts)
- 500mm fixed vertical spacing
- Automatic check for host-bound tags
- Detailed logging system

## Technical Details

- .NET Framework 4.8
- Revit 2022 API
- C# 9.0
- Transaction management
- Metric system support (mm)

## Usage

1. Open "BIRD testTask" tab in Revit
2. Click "Auto Arrange" button
3. Select the tags you want to arrange
4. Specify the starting point

## Debugging

Log file location: `%AppData%/TagsOrdering.log`

## Developer

- **BT**
- GitHub: BTankut
