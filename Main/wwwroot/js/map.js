var myGeoObject;
var circle;

ymaps.ready(initFunc);

function init(jsShot) {

  // Initialize the map
  myMap = new ymaps.Map("map", {
    center: [jsShot.Latitude, jsShot.Longitude],
    zoom: jsShot.Zoom,
    controls: []
  });

  // Create the GeoObject with initial coordinates
  myGeoObject = new ymaps.GeoObject(
    { geometry: { type: "Point", coordinates: [jsShot.Latitude, jsShot.Longitude] } },
    { preset: 'islands#blackStretchyIcon', draggable: true }
  );

  // Add the GeoObject to the map
  myMap.geoObjects.add(myGeoObject);

  myMap.events.add('click', function (e) {
    var coords = e.get('coords');
    myGeoObject.geometry.setCoordinates(coords);
    myGeoObject.properties.set('iconContent', '');
    document.all['LocationId'].selectedIndex = 0;
    document.all['Latitude'].value = coords[0];
    document.all['Longitude'].value = coords[1];
    document.all['Zoom'].value = myMap.getZoom();
  });

  myGeoObject.events.add(['wheel'],
    function (e) {
      document.all['Zoom'].value = myMap.getZoom();
    });

  myMap.geoObjects.add(myGeoObject);

}

function init_edit(jsModel) {
  myMap = new ymaps.Map("map", {
    center: [jsModel.Latitude, jsModel.Longitude],
    zoom: jsModel.Zoom,
    controls: []
  }),

    myGeoObject = new ymaps.GeoObject(
      { geometry: { type: "Point", coordinates: [jsModel.Latitude, jsModel.Longitude] }, properties: { iconContent: jsModel.Name } },
      { preset: 'islands#blackStretchyIcon', draggable: true }
    )

  circle = new ymaps.Circle([[jsModel.Latitude, jsModel.Longitude], jsModel.LocationPrecisionMeters], {}, {
    geodesic: true
  });

  myMap.events.add('click', function (e) {
    var coords = e.get('coords');
    myGeoObject.geometry.setCoordinates(coords);
    circle.geometry.setCoordinates(coords);
    document.all['Latitude'].value = coords[0];
    document.all['Longitude'].value = coords[1];
  });

  myMap.events.add('wheel', function (e) {
    var coords = e.get('coords');
    document.all['Zoom'].value = myMap.getZoom();
  });

  myGeoObject.events.add(['mapchange', 'dragend'],
    function (e) {
      coords = myGeoObject.geometry.getCoordinates();
      circle.geometry.setCoordinates(coords);
      document.all['Latitude'].value = coords[0];
      document.all['Longitude'].value = coords[1];
      document.all['Zoom'].value = myMap.getZoom();
    }
  );

  myMap.geoObjects.add(myGeoObject);

}

function show(i, jsModel) {

  var shift = 2;

  if (i == 0) {
    myMap.setCenter([jsModel[i - shift].Latitude, jsModel[i - shift].Longitude]);
    myMap.setZoom(jsModel[i - shift].Zoom);
    myMap.geoObjects.removeAll();
  }
  if (myGeoObject == null) {
    myGeoObject = new ymaps.GeoObject(
      { geometry: { type: "Point", coordinates: [jsModel[i - shift].Latitude, jsModel[i - shift].Longitude] }, properties: { iconContent: jsModel[i - shift].Name } },
      { preset: 'islands#blackStretchyIcon', draggable: true }
    );
    myMap.geoObjects.add(myGeoObject);
  }
  myGeoObject.geometry.setCoordinates([jsModel[i - shift].Latitude, jsModel[i - shift].Longitude]);
  myGeoObject.properties.set('iconContent', jsModel[i - shift].Name);
  myMap.setCenter([jsModel[i - shift].Latitude, jsModel[i - shift].Longitude]);
  myMap.setZoom(jsModel[i - shift].Zoom);
  document.all['Latitude'].value = jsModel[i - shift].Latitude;
  document.all['Longitude'].value = jsModel[i - shift].Longitude;
  document.all['Zoom'].value = jsModel[i - shift].Zoom;
}

