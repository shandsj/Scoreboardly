# Get Scoreboard by ID
Gets the scoreboard with the specified identifier from the service.

**URL** : `api/scoreboards/:id`

**Method** : `GET`

**Auth required** : NO

**Permissions required** : NO

## Success Response

**Code** : `200 OK`

**Content examples**

A response with a single scoreboard.

```json
[
  {
    "id": "b8f4e5bb-e5e3-43ad-b35a-456ecaeb80f5",
    "name": "My Game",
    "maximumScores": 3,
    "scores": [
      {
        "playerName": "Jason",
        "score": 660
      },
      {
        "playerName": "Jarrod",
        "score": 650
      },
      {
        "playerName": "Matthew",
        "score": 640
      }
    ]
  }
]
```

## Error Response

**Condition** : If the id provided is invalid.

**Code** : `404 NOT FOUND`

**Content** : `{}`