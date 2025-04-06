var myGeoObject;
var circle;

ymaps.ready(function () {
  init(jsShot);
});

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

function setdate(year) {
  if (year < 1000) {
    document.all["DateStart"].value = year + "0-01-01";
    document.all["DateEnd"].value = year + "9-12-31";
  } else {
    document.all["DateStart"].value = year + "-01-01";
    document.all["DateEnd"].value = year + "-12-31";
  }
}