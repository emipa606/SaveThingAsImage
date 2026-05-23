# Copilot Instructions for the "Save Thing As Image" Mod

## Mod Overview and Purpose

**Mod Name**: Save Thing As Image

**Mod Description**: 
This mod was developed to address a common frustration among modders: capturing high-quality in-game images of textures, especially when they feature dynamic colors or shadows. In many cases, the original game texture sources do not reflect these in-game variations, making it difficult to create accurate visual representations for mod previews or documentation.

This mod introduces a new debug action called "Save Thing as image," allowing users to click on any in-game "Thing" (including pawns, items, and buildings) and export its graphic exactly as it appears in-game. This functionality is particularly useful for modders and players who wish to create accurate previews or track the visual appearance of assets in RimWorld.

## Key Features and Systems

- **Debug Action Integration**: The mod adds a new debug mode function, enhancing the utility of the debugging interface within RimWorld.
- **Image Export Functionality**: By selecting a "Thing" in the game, users can export its visual representation with all dynamic effects applied, capturing the true in-game appearance.
- **Broad Asset Compatibility**: Supports exporting images of various in-game objects, including pawns, items, and structures.

## Coding Patterns and Conventions

- **Class Structure**: The mod primarily extends RimWorld's debugging functionality, which involves static classes and methods for ease of access.
- **Naming Conventions**: Classes and methods are named using PascalCase to align with C# conventions. For example, class names like `DebugSpawning` and method names typically start with verbs describing their action, promoting clarity and readability.
- **Code Readability and Maintainability**: Aim to keep the code modular and well-documented with comments detailing the purpose of complex sections, ensuring future modders can easily understand and build upon the mod.

## XML Integration

- XML may be used for defining additional settings or configurations the mod might need, like enabling or disabling specific features.
- If applicable, ensure XML files are well-formed and adhere to RimWorld's modding framework standards.

## Harmony Patching

- **Harmony**: To extend or modify the behavior of existing game methods without altering the source code directly, Harmony patches provide a non-intrusive method for mod integration, alongside RimWorld's existing codebase.
- **Usage of Harmony**: Since the mod introduces a new debug action, Harmony isn't heavily relied on for this specific feature unless further game behavior modifications are required.
- **Future Expansions**: If you plan to modify existing game behavior, consider using Harmony for safe and effective method patching. Ensure patches are targeted carefully to avoid conflicts with other mods.

## Suggestions for Copilot

- **Assist with Method Suggestions**: Given the functional structure of `DebugSpawning`, leveraging Copilot to suggest method signatures or helper functions could speed up development.
- **Improve Readability and Efficiency**: Use Copilot to propose potential optimizations or refactoring opportunities for existing methods, ensuring the code remains effective and efficient.
- **Generate Documentation Comments**: Suggest XML documentation comments for public classes and methods, enhancing both auto-generated documentation and codebase readability.
- **Automate Test Case Suggestions**: While mod development, suggest pseudocode or test case structures to ensure robust feature implementation and maintenance.

By following these guidelines, you can effectively leverage GitHub Copilot to enhance and maintain the "Save Thing As Image" mod, ensuring it's both powerful and easy for other developers to understand and modify.

## Project Solution Guidelines
- Relevant mod XML files are included as Solution Items under the solution folder named XML, these can be read and modified from within the solution.
- Use these in-solution XML files as the primary files for reference and modification.
- The `.github/copilot-instructions.md` file is included in the solution under the `.github` solution folder, so it should be read/modified from within the solution instead of using paths outside the solution. Update this file once only, as it and the parent-path solution reference point to the same file in this workspace.
- When making functional changes in this mod, ensure the documented features stay in sync with implementation; use the in-solution `.github` copy as the primary file.
- In the solution is also a project called Assembly-CSharp, containing a read-only version of the decompiled game source, for reference and debugging purposes.
- For any new documentation, update this copilot-instructions.md file rather than creating separate documentation files.
