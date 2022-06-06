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

Environment variables used in application:

DB_CONNECTION   - sample database connection
STORAGE_DIR     - directory for content storage

sample:
DB_CONNECTION=Host=localhost;Username=postgres;Password=password;Database=svema;Port=5432
STORAGE_DIR=D:\SVEMA\

How to update database after model change:

dotnet ef database drop     - clean database - for non prod
dotnet ef migrations add    - make migrations
dotnet ef database update   - apply migrations

