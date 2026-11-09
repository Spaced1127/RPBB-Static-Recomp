# Icons

`rpbb.ico` and `rpbb-launcher.ico` are derived from the official North American
GameCube box art for **Rocket Power: Beach Bandits**, downloaded from the LaunchBox
Games Database:

- Box front: https://images.launchbox-app.com/2865798e-06ad-4cda-a3f4-8b4091ad1200.png
- Database entry: https://gamesdb.launchbox-app.com/games/images/30861-rocket-power-beach-bandits

The raw cover is not part of this repository or the release. Only the icon resources
are embedded in `rpbb.exe` and `rpbb-launcher.exe`. To regenerate the icons, place the
box front at `work/cover-front.png` and run:

```
python tools/make_icons_from_cover.py
```
