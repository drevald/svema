# Svema project
Scanned films database UI

User should be able to:
---
* upload scanned photos
* group photos by films 
* use photos deployed somewhere else (Google Photo, Flickr, etc)
* add comments to separate photos
* add comments to films
* set filming location for separate photos and whole films with different precesion (somewhere in Moscow, somewhere in Egypt, etc)
* set filming dates with different precision (approx decade, approx year, approx date, etc)

Would be nice to have:
---
* persons identification and search
* keep persons database
* other objects identification and search (find all cats for example) 

dotnet ef database drop     - clean database
dotnet ef migrations add    - make migrations
dotnet ef database update   - apply migrations

==== DEPLOYEMNT TO HEROKU:
heroku container:login
heroku container:push web --app svema
heroku container:release web --app svema

==== .env file format
DATABASE_URL=postgres://postgres:password@postgres:5433/svema
DB_CONNECTION=Host=localhost;Username=postgres;Password=password;Database=svema;Port=5433
STORAGE_DIR=/Users/denis/Desktop/SVEMA/
PORT=8888

INSERT INTO storages (user_id,auth_token,refresh_token,provider,root) VALUES
	 (1,NULL,NULL,'LocalDisk','/storage/svema');

 