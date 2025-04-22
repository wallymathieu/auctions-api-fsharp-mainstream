import time
from locust import HttpUser, task, between
from datetime import datetime, timedelta, timezone

def convert_to_iso_format(dt):
    return dt.isoformat()[:-9]+'Z'

class CreateAuctions(HttpUser):
    wait_time = between(1, 5) 
    @task(3)
    def view_items(self):
        now = datetime.now(timezone.utc)
        ends_at = now + timedelta(hours=2)

        # Convert to ISO format strings
        starts_at_iso = convert_to_iso_format(now)
        ends_at_iso = convert_to_iso_format(ends_at)

        print(f"Starts at: {starts_at_iso}, Ends at: {ends_at_iso}")
        for item_id in range(10000):
            self.client.post(f"/auction", name="/auction", json={
                "startsAt": starts_at_iso,
                "endsAt": ends_at_iso,
                "title": "Some auction",
                "currency": "VAC"
            }, headers={
                "x-jwt-payload": "eyJzdWIiOiJhMSIsICJuYW1lIjoiVGVzdCIsICJ1X3R5cCI6IjAifQo=",
                "Content-Type": "application/json"
            })


# some unit tests:
if __name__ == "__main__":
    now = datetime(2020, 1, 8, 6, 6, 24, 260810, tzinfo=timezone.utc)
    ends_at = now + timedelta(hours=2)
    # Convert to ISO format strings
    starts_at_iso = convert_to_iso_format(now)
    ends_at_iso = convert_to_iso_format(ends_at)

    print(f"Starts at: {starts_at_iso}, Ends at: {ends_at_iso}")
    # Test the conversion
    assert starts_at_iso == "2020-01-08T06:06:24.260Z"
    assert ends_at_iso == "2020-01-08T08:06:24.260Z"