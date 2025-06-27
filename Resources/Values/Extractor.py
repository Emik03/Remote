# SPDX-License-Identifier: MPL-2.0
# Thanks to Darius for writing this for me: https://github.com/itsMapleLeaf/
import os
from pathlib import Path
import subprocess
from tempfile import NamedTemporaryFile, TemporaryDirectory
from zipfile import ZipFile

apworld_path = os.environ.get("APWORLD_PATH")
if not apworld_path:
    raise Exception("Missing APWORLD_PATH environment variable")

archipelago_repo_path = os.environ.get("ARCHIPELAGO_REPO_PATH")
if not archipelago_repo_path:
    raise Exception("Missing ARCHIPELAGO_REPO_PATH environment variable")

# pass something like `DEBUG_INDENT="  "` to get indented output for debugging
debug_indent = os.environ.get("DEBUG_INDENT")

apworld_path = Path(apworld_path)
apworld_zip = ZipFile(apworld_path, mode="r")

data_loader_source = """from . import Data
import json
import os

debug_indent = os.environ.get("DEBUG_INDENT")

print(json.dumps({
    "game.json": Data.game_table,
    "items.json": Data.item_table,
    "locations.json": Data.location_table,
    "regions.json": Data.region_table,
    "categories.json": Data.category_table,
    "options.json": Data.option_table,
    "meta.json": Data.meta_table,
}, indent=debug_indent))
"""

with TemporaryDirectory() as temp_world_folder:
    apworld_zip.extractall(temp_world_folder)
    apworld_name = os.listdir(temp_world_folder)[0]

    with NamedTemporaryFile(
        mode="w",
        dir=Path(temp_world_folder) / apworld_name,
        suffix=".py",
        delete_on_close=False,
    ) as data_loader_file:
        data_loader_file.write(data_loader_source)
        data_loader_file.close()

        (data_loader_module_name, _) = os.path.splitext(
            os.path.basename(data_loader_file.name)
        )

        subprocess_env = {
            **os.environ,
            "PYTHONPATH": archipelago_repo_path,
        }

        if debug_indent:
            subprocess_env["DEBUG_INDENT"] = debug_indent

        subprocess.run(
            ["python", "-m", f"{apworld_name}.{data_loader_module_name}"],
            cwd=temp_world_folder,
            env=subprocess_env,
            capture_output=False,
        )
