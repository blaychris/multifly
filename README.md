
# multifly
a mobile multiplication game

# Fly Math Game

## Overview
Fly Math Game is an engaging mobile game developed using Godot Engine, where players must solve multiplication problems to defeat flying enemies. The game features a series of levels, each with increasing difficulty, and allows players to customize their experience.

## Features
- **Main Menu**: Navigate to start the game, select levels, view high scores, and customize settings.
- **Levels**: Progress through a series of stages, each with locked levels and star-based completion.
- **High Scores**: Track and display the best scores achieved by players.
- **Customization**: Personalize game settings and player characters.
- **Gameplay**: Solve multiplication problems to defeat flies before they reach the player.

## Gameplay Mechanics
- The game is strictly in portrait mode.
- A floating numpad (0-9) occupies half of the screen for player input.
- Players start with one heart; flies will diminish this heart if they reach the player.
- Each fly has two numbers displayed on its eyes, representing a multiplication problem.
- Players must select a fly to activate the numpad and input the correct product.
- Correct answers cause the fly to explode, and new flies will spawn with different multipliers.
- The stage is cleared when all flies are defeated or the player's heart is consumed.

## Project Structure
```
fly-math-game
├── project.godot
├── README.md
├── scenes
│   ├── MainMenu.tscn
│   ├── LevelSelect.tscn
│   ├── HighScores.tscn
│   ├── Customize.tscn
│   ├── Gameplay.tscn
│   ├── GameOver.tscn
│   ├── StageClear.tscn
│   ├── ui
│   │   ├── FloatingNumpad.tscn
│   │   ├── HeartDisplay.tscn
│   │   └── StarDisplay.tscn
│   └── actors
│       ├── Player.tscn
│       └── Fly.tscn
├── scripts
│   ├── MainMenu.cs
│   ├── LevelSelect.cs
│   ├── HighScores.cs
│   ├── Customize.cs
│   ├── Gameplay.cs
│   ├── GameManager.cs
│   ├── StageManager.cs
│   ├── SaveManager.cs
│   ├── ui
│   │   ├── FloatingNumpad.cs
│   │   ├── HeartDisplay.cs
│   │   └── StarDisplay.cs
│   └── actors
│       ├── Player.cs
│       └── Fly.cs
├── data
│   ├── stages
│   │   └── stage_01.json
│   └── save
│       └── default.save
├── autoload
│   ├── GameState.cs
│   └── AudioManager.cs
└── android
    └── export_presets.cfg
```

## Setup Instructions
1. Clone the repository to your local machine.
2. Open the project in Godot Engine.
3. Configure the project settings as needed.
4. Run the project to start playing!

## Contribution
Feel free to contribute to the project by submitting issues or pull requests. Your feedback and suggestions are welcome!
