function selectAll() {
    for (i=0; i<jsShots.length; i++) {
        document.all["Shots["+i+"].IsChecked"][0].checked = true;
        document.all["Shots["+i+"].IsChecked"][1].checked = true;
    }
}

function deselectAll() {
    for (i=0; i<jsShots.length; i++) {
        document.all["Shots["+i+"].IsChecked"][0].checked = false;
        document.all["Shots["+i+"].IsChecked"][1].checked = false;
    }
}