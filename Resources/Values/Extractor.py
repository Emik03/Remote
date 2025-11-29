# SPDX-License-Identifier: MPL-2.0
# Thanks to Darius for writing this for me: https://github.com/itsMapleLeaf/
import importlib
import importlib.util
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
    world_src_root = Path(temp_world_folder) / apworld_name

    module_file_path = world_src_root / f"Data.py"

    # key by the manual project name,
    # so it doesn't cache other manual project modules by the same name
    module_key = f"manual_data_{apworld_name}"

    module_spec = importlib.util.spec_from_file_location(
        name=module_key,
        location=module_file_path,
        submodule_search_locations=[str(world_src_root)],
    )

    if not module_spec or not module_spec.loader:
        raise Exception(f"Failed to create module spec for {module_file_path}")

    data_module = importlib.util.module_from_spec(module_spec)
    sys.modules[module_key] = data_module

    original_sys_path = sys.path.copy()
    try:
        sys.path.append(archipelago_repo_path)
        module_spec.loader.exec_module(data_module)
    finally:
        sys.path = original_sys_path

    json.dump(
        {
            "game.json": data_module.game_table,
            "items.json": data_module.item_table,
            "locations.json": data_module.location_table,
            "regions.json": data_module.region_table,
            "categories.json": (
                data_module.category_table
                if hasattr(data_module, "category_table")
                else None
            ),
            "options.json": (
                data_module.option_table
                if hasattr(data_module, "option_table")
                else None
            ),
            "meta.json": (
                data_module.meta_table if hasattr(data_module, "meta_table") else None
            ),
        },
        fp=sys.stdout,
        indent=debug_indent,
    )
