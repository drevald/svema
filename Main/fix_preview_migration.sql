-- Remove the migration from history
DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251202193041_AddPersonPreview';

-- Add the column
ALTER TABLE persons ADD COLUMN IF NOT EXISTS preview bytea;

-- Re-add the migration to history
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20251202193041_AddPersonPreview', '6.0.5');
