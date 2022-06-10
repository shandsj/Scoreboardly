# Get All Scoreboards
Gets the available scoreboards from the service.

**URL** : `api/scoreboards`

**Method** : `GET`

**Auth required** : NO

**Permissions required** : NO

**Data** : `{}`

## Success Response

**Code** : `200 OK`

**Content examples**

A response with a single scoreboard.

```json
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
```

A response with multiple scoreboards.

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
  },
  {
    "id": "06ef3cb1-445d-4895-a753-ec41571e2a84",
    "name": "My Other Game",
    "maximumScores": 5,
    "scores": [
      {
        "playerName": "Matthew",
        "score": 60
      },
      {
        "playerName": "Jason",
        "score": 50
      }
    ]
  }
]
```