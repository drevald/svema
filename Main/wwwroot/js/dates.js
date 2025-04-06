function setdate(year) {
    if (year < 1000) {
      document.all["DateStart"].value = year + "0-01-01";
      document.all["DateEnd"].value = year + "9-12-31";
    } else {
      document.all["DateStart"].value = year + "-01-01";
      document.all["DateEnd"].value = year + "-12-31";
    }
  }