The API runs on the same port as the normal game server. This is easier to deploy instead of a dedicated port, but has some limitations.

To use the API the client sends only one texual JSON object to the server and might get a JSON object back (if the request is valid).

The first 20 bytes of the request JSON are constant `{"API_JSON_REQUEST":`,
to fill up and exactly match a complete normal game packet header (to identify and separate it from other server traffic).

A complete request can have a size of up to 512 characters (arbitrary limit that could be increased if needed).

---

Every request to the server needs to be authorized by containing a secret token.
The token and its permissions are configured in the `settings.json`.
There can be several tokens with different permission sets.

IP addresses that provide invalid requests or token values, are automatically blocked after 5 such requests until the next server restart.
(This is mainly there to prevent agains brute force attacks that try to guess the token).

---

Currently available `Type` of requests:
- `Permissions`: lists all permissions the token in use has (this request is always possible and doesn't require an extra permission).
- `Status`: outputs all Settings, Players and Player properties the token has explicit permissions for.
- `Command`: passes an command to the CommandHandler and returns its output. Every command needs to be permitted individually.

Specific settings and commands aren't hardcoded, but the API should automatically work for future extensions on both.
The server operator only needs to add the new permissions for the new commands or settings that they want to whitelist to the `settings.json`.

The possible player status permissions are hardcoded though:
- `Status/Players`
- `Status/Players/ID`
- `Status/Players/Name`
- `Status/Players/GameMode`
- `Status/Players/Kingdom`
- `Status/Players/Stage`
- `Status/Players/Scenario`
- `Status/Players/Position`
- `Status/Players/Rotation`
- `Status/Players/Tagged`
- `Status/Players/Costume`
- `Status/Players/Capture`
- `Status/Players/Is2D`
- `Status/Players/IPv4`

---

Example for the `settings.json`:
```json
"JsonApi": {
  "Enabled": true,
  "Tokens": {
    "SECRET_TOKEN_12345": [
      "Status/Settings/Server/MaxPlayers",
      "Status/Settings/Scenario/MergeEnabled",
      "Status/Settings/Shines/Enabled",
      "Status/Settings/PersistShines/Enabled",
      "Status/Players",
      "Status/Players/Name",
      "Status/Players/Stage",
      "Status/Players/Costume",
      "Commands",
      "Commands/list",
      "Commands/sendall"
    ]
  }
}
```

---

Example request (e.g. with `./test.sh Command sendall mush`):
```json
{"API_JSON_REQUEST":{"Token":"SECRET_TOKEN_12345","Type":"Command","Data":"sendall mush"}}
```

Example `hexdump -C` response:
```
00000000  7b 22 4f 75 74 70 75 74  22 3a 5b 22 53 65 6e 74  |{"Output":["Sent|
00000010  20 70 6c 61 79 65 72 73  20 74 6f 20 50 65 61 63  | players to Peac|
00000020  68 57 6f 72 6c 64 48 6f  6d 65 53 74 61 67 65 3a  |hWorldHomeStage:|
00000030  2d 31 22 5d 7d                                    |-1"]}|
00000035
```
