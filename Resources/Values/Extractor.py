# SPDX-License-Identifier: MPL-2.0
# Thanks to Darius for writing this for me: https://github.com/itsMapleLeaf/
import importlib
import json
import os
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
from zipfile import ZipFile

apworld_path = os.environ.get("APWORLD_PATH")
if not apworld_path:
    raise Exception("Missing APWORLD_PATH environment variable")

archipelago_repo_path = os.environ.get("ARCHIPELAGO_REPO_PATH")
if not archipelago_repo_path:
    raise Exception("Missing ARCHIPELAGO_REPO_PATH environment variable")

# pass something like `DEBUG_INDENT="  "` to get indented output for debugging
debug_indent = os.environ.get("DEBUG_INDENT")
if debug_indent and debug_indent.isdigit():
    debug_indent = int(debug_indent)

apworld_path = Path(apworld_path)
apworld_zip = ZipFile(apworld_path, mode="r")

with TemporaryDirectory() as temp_world_folder:
    apworld_zip.extractall(temp_world_folder)
    apworld_name = os.listdir(temp_world_folder)[0]

    original_path = sys.path
    try:
        sys.path += [temp_world_folder, archipelago_repo_path]
        data_module = importlib.import_module(".Data", apworld_name)
    finally:
        sys.path = original_path

    json.dump(
        {
            "game.json": data_module.game_table,
            "items.json": data_module.item_table,
            "locations.json": data_module.location_table,
            "regions.json": data_module.region_table,
            "categories.json": data_module.category_table,
            "options.json": data_module.option_table,
            "meta.json": data_module.meta_table,
        },
        fp=sys.stdout,
        indent=debug_indent,
    )
