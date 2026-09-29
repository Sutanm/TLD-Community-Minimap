"""Print the mountain_town calibration landmarks.

These are the only places where a world position and a community-map pixel are
known to correspond, so they are the reference for whether the vanilla base map's
content agrees with the calibrated world extent.
"""
import json

PATH = r"D:\CommunityMinimap-Workspace\calibrations.json"

data = json.load(open(PATH, encoding="utf-8"))
for m in data["maps"]:
    if m["mapId"] != "mountain_town":
        continue
    print(f"mountain_town image {m['imageWidth']}x{m['imageHeight']}")
    for p in m["points"]:
        print("  {:<28} world=({:8.1f},{:8.1f})  map=({:8.1f},{:8.1f})".format(
            p["label"], p["worldX"], p["worldZ"], p["mapX"], p["mapY"]))
