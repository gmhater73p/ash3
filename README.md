# ash3

(from 2024-2025)

This was a never-finished rewrite of [Ash](https://github.com/gmhater73/ash-public) in C#, using Discord.NET and other libraries, as opposed to JavaScript with Node.js and discord.js.

See ash-public to know all features that were supposed to be ported to ash3.

Rewriting it in C#, a vastly more statically-typed language, (even more so than TypeScript), proved to make code maintenance and adding new features much easier - even in this intermediary stage where not all of the pre-existing features have yet been ported over.

**Features:**
* Custom entity data system and database layer using SQLite
* Activity system that provides an interface that is agnostic between Discord text and slash commands (different methods of presentation)
* Flexible command handling system with a custom parser that accepts partials and identifiers to distinguish between different types of entities

**Purpose? (previous experience)**

Ash was created to serve StormLands, a Discord community centered around a multiplayer game that peaked at ~1,100 members.

As one of three administrators and designated systems maintainer, I created Ash to automate some functions that were critical to us, such as the economy, Steam account linking, server instance management, vehicle XML file analysis, and more.

ash-public was open-sourced when we disbanded StormLands in September 2024.

The development of Ash 3 started in early 2024 and continued through 2025 for a proposed reboot that never materialized.
