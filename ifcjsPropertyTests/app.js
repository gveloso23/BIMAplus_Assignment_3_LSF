import { Color } from 'three';
import { IfcViewerAPI } from 'web-ifc-viewer';

const container = document.getElementById('viewer-container');
const viewer = new IfcViewerAPI({ container, backgroundColor: new Color(0xffffff) });
viewer.grid.setGrid();
viewer.axes.setAxes();

async function loadIfc(url) {
    await viewer.IFC.setWasmPath("../../../");
    const model = await viewer.IFC.loadIfcUrl(url);
    await viewer.shadowDropper.renderShadow(model.modelID);
    viewer.context.renderer.postProduction.active = true;
}

loadIfc('../../../IFC/lsf.ifc');

//Properties menu

window.onmousemove = () => viewer.IFC.selector.prePickIfcItem();

window.ondblclick = async () => {
    const result = await viewer.IFC.selector.highlightIfcItem(false,true);
    if (!result) return;
    const { modelID, id } = result;
    const props = await viewer.IFC.getProperties(modelID, id, true, true);
    createPropertiesMenu(props);
};
const propsGUI = document.getElementById("ifc-property-menu-root");

function createPropertiesMenu(properties) {
    console.log(properties);
    removeAllChildren(propsGUI);

    const psets = properties.psets[0].HasProperties;
  for (let key in psets) {
    createPropertyEntry(psets[key].Name.value, psets[key].NominalValue.value);
  }

}

function createPropertyEntry(key, value) {
    const propContainer = document.createElement("div");
    propContainer.classList.add("ifc-property-item");

    if(value === null || value === undefined) value = "undefined";
    else if(value.value) value = value.value;

    const keyElement = document.createElement("div");
    keyElement.textContent = key;
    propContainer.appendChild(keyElement);

    const valueElement = document.createElement("div");
    valueElement.classList.add("ifc-property-value");
    valueElement.textContent = value;
    propContainer.appendChild(valueElement);

    propsGUI.appendChild(propContainer);
}

function removeAllChildren(element) {
    while (element.firstChild) {
        element.removeChild(element.firstChild);
    }
}