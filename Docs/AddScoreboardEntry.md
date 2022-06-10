# Add Scoreboard Entry
Adds a scoreboard entry to a scoreboard in the service.

**URL** : `api/scoreboards/:id`

**Method** : `PUT`

**Auth required** : NO

**Permissions required** : NO

**Data examples**

```json
{
  "playerName": "Jason",
  "score": "30"
}
```

## Success Response

**Condition** : Data provided is valid.

**Code** : `200 OK`

**Content** : `{}`

## Error Response

**Condition** : If the request was malformed.

**Code** : `400 BAD REQUEST`

**Content** : `{}`

### Or

**Condition** : If the score was not high enough, or wasn't enough room on the scoreboard.

**Code** : `409 CONFLICT`

**Content** : `{}`