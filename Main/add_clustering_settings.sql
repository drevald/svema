-- Migration: Add ClusteringSettings table

CREATE TABLE IF NOT EXISTS clustering_settings (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    preset INTEGER NOT NULL DEFAULT 1, -- 0=Conservative, 1=Balanced, 2=Aggressive, 3=NoiseTolerant, 4=Custom
    similarity_threshold REAL NOT NULL DEFAULT 0.23,
    min_faces_per_person INTEGER NOT NULL DEFAULT 2,
    min_face_size INTEGER NOT NULL DEFAULT 80,
    min_face_quality REAL NOT NULL DEFAULT 0.3,
    auto_merge_threshold REAL NOT NULL DEFAULT 0.80,
    is_face_processing_suspended BOOLEAN NOT NULL DEFAULT false,
    UNIQUE(user_id)
);

CREATE INDEX IF NOT EXISTS idx_clustering_settings_user_id ON clustering_settings(user_id);
