
-- Table: mapNode
CREATE TABLE mapNode ( 
    id         INTEGER PRIMARY KEY AUTOINCREMENT
                       NOT NULL
                       UNIQUE,
    osm_id     INTEGER,
    latitude   REAL,
    longitude  REAL,
    name       TEXT,
    type       TEXT,
    subtype    TEXT,
    subsubtype TEXT 
);


-- Table: mapFeature
CREATE TABLE mapFeature ( 
    id         INTEGER PRIMARY KEY AUTOINCREMENT
                       NOT NULL
                       UNIQUE,
    osm_id     INTEGER UNIQUE,
    name       TEXT,
    type       TEXT,
    subtype    TEXT,
    subsubtype TEXT 
);


-- Table: mapFeatureNode
CREATE TABLE mapFeatureNode ( 
    id          INTEGER PRIMARY KEY AUTOINCREMENT
                        NOT NULL
                        UNIQUE,
    feature_id  INTEGER NOT NULL
                        REFERENCES mapFeature ( id ),
    node_osm_id INTEGER NOT NULL
                        REFERENCES mapNode ( id ) 
);


-- Table: mapShapeFile
CREATE TABLE mapShapeFile ( 
    id   INTEGER PRIMARY KEY AUTOINCREMENT
                 NOT NULL
                 UNIQUE,
    path TEXT 
);


-- Table: mapShape
CREATE TABLE mapShape ( 
    id      INTEGER PRIMARY KEY AUTOINCREMENT
                    NOT NULL
                    UNIQUE,
    node_id INTEGER NOT NULL
                    REFERENCES mapNode ( id ),
    file_id INTEGER NOT NULL
                    REFERENCES mapShapeFile ( id ),
    x       REAL,
    y       REAL,
    z       REAL,
    rx      REAL,
    ry      REAL,
    rz      REAL,
    rw      REAL,
    sx      REAL,
    sy      REAL,
    sz      REAL 
);


-- Table: erRoad
CREATE TABLE erRoad ( 
    id           INTEGER PRIMARY KEY AUTOINCREMENT
                         NOT NULL
                         UNIQUE,
    feature_id   INTEGER,
    road_type_id INTEGER 
);


-- Table: erRoadType
CREATE TABLE erRoadType ( 
    id       INTEGER PRIMARY KEY AUTOINCREMENT
                     NOT NULL
                     UNIQUE,
    material TEXT,
    width    REAL,
    height   REAL 
);

INSERT INTO [erRoadType] ([id], [material], [width], [height]) VALUES (1, 'Materials/roads/road material', 5.0, null);
INSERT INTO [erRoadType] ([id], [material], [width], [height]) VALUES (2, 'Materials/roads/road material', 10.0, null);
INSERT INTO [erRoadType] ([id], [material], [width], [height]) VALUES (3, 'Materials/roads/dirt material', 4.0, null);
INSERT INTO [erRoadType] ([id], [material], [width], [height]) VALUES (4, 'Materials/sidewalks/sidewalk', 1.5, null);
INSERT INTO [erRoadType] ([id], [material], [width], [height]) VALUES (5, 'Materials/roads/Dirt Grass', 1.5, null);

-- Table: erConnectionType
CREATE TABLE erConnectionType ( 
    id     INTEGER PRIMARY KEY AUTOINCREMENT
                   NOT NULL
                   UNIQUE,
    prefab TEXT 
);

INSERT INTO [erConnectionType] ([id], [prefab]) VALUES (1, 'Default X Crossing');
INSERT INTO [erConnectionType] ([id], [prefab]) VALUES (2, 'Default T Crossing');

-- Table: erRoadMarker
CREATE TABLE erRoadMarker ( 
    id           INTEGER PRIMARY KEY AUTOINCREMENT
                         NOT NULL
                         UNIQUE,
    map_node_id  INTEGER,
    road_id      INTEGER,
    pos_x        REAL    DEFAULT ( 0 ),
    pos_y        REAL    DEFAULT ( 0 ),
    pos_z        REAL    DEFAULT ( 0 ),
    rot_x        REAL    DEFAULT ( 0 ),
    rot_y        REAL    DEFAULT ( 0 ),
    rot_z        REAL    DEFAULT ( 0 ),
    control_type INTEGER DEFAULT ( 0 ) 
);


-- Table: erConnection
CREATE TABLE erConnection ( 
    id                 INTEGER PRIMARY KEY AUTOINCREMENT
                               NOT NULL
                               UNIQUE,
    connection_type_id INTEGER REFERENCES erConnectionType ( id ),
    node_0             INTEGER DEFAULT ( 0 ),
    node_1             INTEGER DEFAULT ( 0 ),
    node_2             INTEGER DEFAULT ( 0 ),
    node_3             INTEGER DEFAULT ( 0 ),
    pos_x              REAL    DEFAULT ( 0 ),
    pos_y              REAL    DEFAULT ( 0 ),
    pos_z              REAL    DEFAULT ( 0 ),
    rot_x              REAL    DEFAULT ( 0 ),
    rot_y              REAL    DEFAULT ( 0 ),
    rot_z              REAL    DEFAULT ( 0 ) 
);


-- Index: idx_mapNode_coords
CREATE INDEX idx_mapNode_coords ON mapNode ( 
    latitude  ASC,
    longitude ASC 
);


-- Index: idx_mapNode_osm_id
CREATE INDEX idx_mapNode_osm_id ON mapNode ( 
    osm_id ASC 
);


-- Index: idx_mapNode
CREATE INDEX idx_mapNode ON mapNode ( 
    name ASC 
);


-- Index: idx_mapFeature_osm_id
CREATE UNIQUE INDEX idx_mapFeature_osm_id ON mapFeature ( 
    osm_id ASC 
);

