jsShots = jsModel.Shots;

function flip() {
    for (let i = 0; i < jsShots.length; i++) {
        const checkbox = document.querySelector(`[name="Shots[${i}].IsChecked"]`);
        if (checkbox && checkbox.checked) {
            const flipInput = document.querySelector(`#Shots_${i}__Flip`);
            if (flipInput) {
                const currentValue = flipInput.value === "true";
                flipInput.value = (!currentValue).toString();
            }
            applyTransform(i);
        }
    }
}

function rotate() {
    for (let i = 0; i < jsShots.length; i++) {
        const checkbox = document.querySelector(`[name="Shots[${i}].IsChecked"]`);
        if (checkbox && checkbox.checked) {
            const rotateInput = document.querySelector(`#Shots_${i}__Rotate`);
            if (rotateInput) {
                const currentValue = parseInt(rotateInput.value, 10) || 0;
                rotateInput.value = (currentValue + 90) % 360;
            }
            applyTransform(i);
        }
    }
}

function applyAllTransformsCheckedOnly() {
    const checkboxes = document.querySelectorAll('input[type="checkbox"][name^="Shots["][name$="].IsChecked"]');
    checkboxes.forEach(checkbox => {
        if (checkbox.checked) {
            const match = checkbox.id.match(/Shots_(\d+)__IsChecked/);
            if (match) {
                const index = match[1];
                applyTransform(index);
            }
        }
    });
}

function applyTransform(index) {
    const shotId = document.getElementById(`Shots_${index}__ShotId`).value;
    const rotateInput = document.getElementById(`Shots_${index}__Rotate`).value;
    const flipInput = document.getElementById(`Shots_${index}__Flip`).value;
    const cardBody = document.querySelector(`#Shots_${index}__Flip`).closest('.card-body');
    const newUrl = `url("/preview?id=${shotId}&flip=${flipInput}&rotate=${rotateInput}")`;
    cardBody.style.backgroundImage = "none";
    cardBody.offsetHeight;
    cardBody.style.backgroundImage = newUrl;
}


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