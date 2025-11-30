// Global variables to hold the main marker and circle
var myGeoObject; // Draggable marker on the map
var circle;      // Circle to show location precision

// Wait until Yandex Maps API is ready, then call initFunc
ymaps.ready(initFunc);

/**
 * Initialize a map with multiple placemarks and bounds
 * @param jsShot - object containing map bounds and placemarks
 */
function init_locations(jsModel, isInteractive) {

  // Get bounding coordinates from model
  const north = jsModel.North;
  const south = jsModel.South;
  const east = jsModel.East;
  const west = jsModel.West;

  // Construct map bounds: [[southWestLat, southWestLng], [northEastLat, northEastLng]]
  const bounds = [[south, west], [north, east]];

  // Initialize map with bounds
  myMap = new ymaps.Map("map", {
    bounds: bounds
  }, {
    checkZoomRange: true,
    zoomMargin: [10]
  });

  // Event: When map bounds change (user pans or zooms)
  myMap.events.add('boundschange', function (e) {
    var bounds = myMap.getBounds();
    var south = bounds[0][0]; // South latitude
    var north = bounds[1][0]; // North latitude
    var west = bounds[0][1];  // West longitude
    var east = bounds[1][1];  // East longitude

    // Update form fields
    document.querySelector('#North').value = north;
    document.querySelector('#South').value = south;
    document.querySelector('#East').value = east;
    document.querySelector('#West').value = west;

    if(isInteractive && document.querySelector('input[name="refresh"]')) {
      document.querySelector('input[name="refresh"]').click();
    }

  });

  // Add placemarks from the model
  jsModel.Placemarks.forEach(pm => {
    const placemark = new ymaps.GeoObject({
      geometry: {
        type: "Point",
        coordinates: [pm.Latitude, pm.Longitude]
      },
      properties: {
        iconContent: pm.Label // Label shown on the map
      }
    }, {
      preset: 'islands#invertedGreenClusterIcons',
      draggable: false // Placemark is fixed, not draggable
    });

    // Event: Clicking on a placemark
    placemark.events.add('click', e => {
      const coords = e.get('target').geometry.getCoordinates();

      // Ensure the coordinate input elements exist
      const northEl = document.querySelector('#North');
      const southEl = document.querySelector('#South');
      const eastEl = document.querySelector('#East');
      const westEl = document.querySelector('#West');

      const lat = coords[0];
      const lon = coords[1];
      const delta = 0.01; // small offset in degrees (~1 km)

      if (northEl && southEl && eastEl && westEl) {
        northEl.value = lat + delta;
        southEl.value = lat - delta;
        eastEl.value  = lon + delta;
        westEl.value  = lon - delta;
      } else {
        console.warn('Coordinate input elements not found.');
      }

      // Set map boundaries based on those coordinates
      const bounds = [
        [lat - delta, lon - delta], // southwest corner (South, West)
        [lat + delta, lon + delta]  // northeast corner (North, East)
      ];
      myMap.setBounds(bounds, { checkZoomRange: true, duration: 300 });

    });

    // Add the placemark to the map
    myMap.geoObjects.add(placemark);
  });

  // Enable editing after all placemarks are added (if applicable)
  if (jsModel.EditLocation) {
    enableEditing();
  }

}

/**
 * Initialize a map with a single draggable marker
 * @param jsModel - object with initial marker coordinates and zoom
 */
function init(jsShot) {

  // Initialize map centered at given coordinates
  myMap = new ymaps.Map("map", {
    center: [jsShot.Latitude, jsShot.Longitude],
    zoom: jsShot.Zoom,
    controls: [] // No default controls
  });

  // Create a draggable marker at initial coordinates
  myGeoObject = new ymaps.GeoObject(
    { geometry: { type: "Point", coordinates: [jsShot.Latitude, jsShot.Longitude] } },
    { preset: 'islands#blackStretchyIcon', draggable: true }
  );

  // Add marker to the map
  myMap.geoObjects.add(myGeoObject);

  // Event: Clicking on the map moves the marker
  myMap.events.add('click', function (e) {
    var coords = e.get('coords');
    myGeoObject.geometry.setCoordinates(coords); // Move marker
    myGeoObject.properties.set('iconContent', ''); // Clear label
    document.all['LocationId'].selectedIndex = 0;  // Reset dropdown
    document.all['Latitude'].value = coords[0];
    document.all['Longitude'].value = coords[1];
    document.all['Zoom'].value = myMap.getZoom();  // Update zoom
  });

  // Event: Update zoom field when scrolling
  myGeoObject.events.add(['wheel'], function (e) {
    document.all['Zoom'].value = myMap.getZoom();
  });

  // Ensure marker is added to map
  myMap.geoObjects.add(myGeoObject);
}

/**
 * Initialize a map for editing an existing location with a precision circle
 * @param jsModel - object with marker coordinates, zoom, name, and location precision
 */
function init_edit(jsModel) {
  // Initialize map at given location
  myMap = new ymaps.Map("map", {
    center: [jsModel.Latitude, jsModel.Longitude],
    zoom: jsModel.Zoom,
    controls: []
  });

  // Create draggable marker
  myGeoObject = new ymaps.GeoObject(
    { geometry: { type: "Point", coordinates: [jsModel.Latitude, jsModel.Longitude] }, properties: { iconContent: jsModel.Name } },
    { preset: 'islands#blackStretchyIcon', draggable: true }
  );

  // Create a circle around the marker to show precision
  circle = new ymaps.Circle([[jsModel.Latitude, jsModel.Longitude], jsModel.LocationPrecisionMeters], {}, {
    geodesic: true
  });

  // Event: Clicking on map moves marker and circle
  myMap.events.add('click', function (e) {
    var coords = e.get('coords');
    myGeoObject.geometry.setCoordinates(coords);
    circle.geometry.setCoordinates(coords);
    document.all['Latitude'].value = coords[0];
    document.all['Longitude'].value = coords[1];
  });

  // Event: Update zoom field on scroll
  myMap.events.add('wheel', function (e) {
    document.all['Zoom'].value = myMap.getZoom();
  });

  // Event: Update circle and fields when marker is dragged
  myGeoObject.events.add(['mapchange', 'dragend'], function (e) {
    coords = myGeoObject.geometry.getCoordinates();
    circle.geometry.setCoordinates(coords);
    document.all['Latitude'].value = coords[0];
    document.all['Longitude'].value = coords[1];
    document.all['Zoom'].value = myMap.getZoom();
  });

  // Add marker to the map
  myMap.geoObjects.add(myGeoObject);
}

// Global variable for the green placemark
var greenPlacemark = null;

/**
 * Enables map editing and puts a movable green placemark at map center.
 * Reuses existing placemark if already created.
 */
function enableEditing() {
  if (!myMap) return;

  // Enable basic map behaviors
  myMap.behaviors.enable('drag');
  myMap.behaviors.enable('scrollZoom');

  // Get current map center
  const center = myMap.getCenter();

  if (greenPlacemark) {
    // Reuse existing placemark, just move it to the new center
    greenPlacemark.geometry.setCoordinates(center);
  } else {
    // Create a new green draggable placemark at the center
    greenPlacemark = new ymaps.GeoObject(
      {
        geometry: { type: "Point", coordinates: center },
        properties: { iconContent: '' }
      },
      {
        preset: 'islands#greenDotIcon',
        draggable: true
      }
    );

    // Add it to the map
    myMap.geoObjects.add(greenPlacemark);

    // Optional: handle drag end to update form or log
    greenPlacemark.events.add('dragend', function (e) {
      const coords = greenPlacemark.geometry.getCoordinates();
      console.log('Green placemark moved to:', coords);
      if (document.all['Latitude']) document.all['Latitude'].value = coords[0];
      if (document.all['Longitude']) document.all['Longitude'].value = coords[1];
      if (document.all['Zoom']) document.all['Zoom'].value = myMap.getZoom();
    });
  }
}

function toggleEditing(checkbox) {
  if (checkbox.checked) {
    enableEditing();
  } else {
    disableEditing();
  }
}

/**
 * Disables map editing:
 * - Removes the green placemark
 * - Clears its properties
 * - Optionally disables map editing behaviors
 */
function disableEditing() {
  if (!myMap || !greenPlacemark) return;

  // Remove the green placemark from the map
  myMap.geoObjects.remove(greenPlacemark);

  // Clear all properties and geometry
  greenPlacemark.properties.set({});
  greenPlacemark.geometry.setCoordinates([]);

  // Nullify reference
  greenPlacemark = null;

  // Optionally disable map editing behaviors
  myMap.behaviors.disable('drag');
  myMap.behaviors.disable('scrollZoom');
}


/**
 * Show a specific location from a list of saved locations
 * @param i - index of location
 * @param jsModel - array of location objects
 * @param shift - offset for indexing
 */
function show(i, jsModel, shift) {

  // If first location, reset map center and remove all objects
  if (i == 0) {
    myMap.setCenter([jsModel[i - shift].Latitude, jsModel[i - shift].Longitude]);
    myMap.setZoom(jsModel[i - shift].Zoom);
    myMap.geoObjects.removeAll();
  }

  // If marker doesn't exist, create it
  if (myGeoObject == null) {
    myGeoObject = new ymaps.GeoObject(
      { geometry: { type: "Point", coordinates: [jsModel[i - shift].Latitude, jsModel[i - shift].Longitude] }, properties: { iconContent: jsModel[i - shift].Name } },
      { preset: 'islands#blackStretchyIcon', draggable: true }
    );
    myMap.geoObjects.add(myGeoObject);
  }

  // Update marker coordinates and label
  myGeoObject.geometry.setCoordinates([jsModel[i - shift].Latitude, jsModel[i - shift].Longitude]);
  myGeoObject.properties.set('iconContent', jsModel[i - shift].Name);

  // Center map on marker and set zoom
  myMap.setCenter([jsModel[i - shift].Latitude, jsModel[i - shift].Longitude]);
  myMap.setZoom(jsModel[i - shift].Zoom);

  // Update form fields
  document.all['Latitude'].value = jsModel[i - shift].Latitude;
  document.all['Longitude'].value = jsModel[i - shift].Longitude;
  document.all['Zoom'].value = jsModel[i - shift].Zoom;

  // Update bounding box ±0.01 degrees around marker
  document.querySelector('#North').value = jsModel[i - shift].Latitude + 0.01;
  document.querySelector('#South').value = jsModel[i - shift].Latitude - 0.01;
  document.querySelector('#East').value = jsModel[i - shift].Longitude + 0.01;
  document.querySelector('#West').value = jsModel[i - shift].Longitude - 0.01;

  // Submit the form
  //document.forms[0].submit();
}
