#!/usr/bin/env python3
"""Apply production settings to a published appsettings.json (CI / TezHost)."""

from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--publish-settings", required=True, help="Path to published appsettings.json")
    parser.add_argument("--pos-hub-mode", default=os.environ.get("POS_HUB_MODE", "Standard"))
    parser.add_argument("--shop-name", default=os.environ.get("SHOP_NAME", "Main Shop"))
    args = parser.parse_args()

    path = Path(args.publish_settings)
    if not path.is_file():
        print(f"Published settings not found: {path}", file=sys.stderr)
        return 1

    connection = (os.environ.get("CONNECTION_STRING") or "").strip()
    if not connection:
        print("CONNECTION_STRING environment variable is required.", file=sys.stderr)
        return 1
    if "YOUR_SQL_SERVER" in connection or "YOUR_USER" in connection or "YOUR_PASSWORD" in connection:
        print("CONNECTION_STRING still contains placeholder values.", file=sys.stderr)
        return 1

    central = (os.environ.get("CONNECTION_STRING_CENTRAL") or connection).strip()
    offline = (os.environ.get("CONNECTION_STRING_OFFLINE") or "Data Source=App_Data/pos-offline.db").strip()

    data = json.loads(path.read_text(encoding="utf-8"))
    data.setdefault("ConnectionStrings", {})
    data["ConnectionStrings"]["POS_MT"] = connection
    data["ConnectionStrings"]["POS_MT_Central"] = central
    data["ConnectionStrings"]["POS_MT_Offline"] = offline
    data["ConnectionStrings"].pop("POS_MT_Local", None)

    pos_hub = data.setdefault("PosHub", {})
    if not isinstance(pos_hub, dict):
        pos_hub = {}
        data["PosHub"] = pos_hub
    pos_hub["Mode"] = args.pos_hub_mode
    pos_hub["ShopName"] = args.shop_name
    pos_hub["OfflineSqlitePath"] = "App_Data/pos-offline.db"
    pos_hub["SyncEnabled"] = True

    data["AllowedHosts"] = os.environ.get("ALLOWED_HOSTS", "*")
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(f"Applied production settings to {path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
