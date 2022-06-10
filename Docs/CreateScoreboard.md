# Create Scoreboard
Creates a new scoreboard in the service.

**URL** : `api/scoreboards`

**Method** : `POST`

**Auth required** : NO

**Permissions required** : NO

**Data examples**

```json
{
  "scoreboardId": "b8f4e5bb-e5e3-43ad-b35a-456ecaeb80f5",
  "name": "My Game",
  "maximumScores": 3
}
```

## Success Response

**Condition** : Data provided is valid.

**Code** : `200 OK`

**Content** : `{}`

## Error Response

**Condition** : If provided data is invalid.

**Code** : `400 BAD REQUEST`

**Content** : `{}`