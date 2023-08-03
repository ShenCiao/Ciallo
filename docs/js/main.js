import * as THREE from 'three';
import { MapControls } from "three/addons/controls/MapControls.js";
import Stats from 'three/addons/libs/stats.module'
import {GUI} from './dat.gui.module.js'
const modelNames = ["Monkey", "Totoro"];

// Scene
const scene = new THREE.Scene();

let canvasHolder = document.getElementById('canvas-holder');
canvasHolder.onmousedown = function(e){
  if(e.button == 1 || e.button == 0){
    e.preventDefault();
  }
}
// Apply your desired aspect ratio
var canvasWidth = canvasHolder.clientWidth;
var canvasHeight = canvasWidth * 0.64;
canvasHolder.style.clientHeight = canvasHeight;

// Camera
let camera = new THREE.OrthographicCamera( 
  canvasWidth / - 2, canvasWidth / 2, 
  canvasHeight / 2, canvasHeight / - 2,
  0.0, 1000
  );
camera.position.z = 1;
camera.zoom = 200;
camera.updateProjectionMatrix();

// Renderer
const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setClearColor("#FFFFFF"); // Set background colour
renderer.setSize(canvasWidth, canvasHeight);
canvasHolder.appendChild(renderer.domElement); // Add renderer to HTML as a canvas element

// Make Canvas Responsive
window.addEventListener("resize", () => {
  var canvasWidth = canvasHolder.clientWidth;
  var canvasHeight = canvasWidth * 0.64;
  renderer.setSize(canvasWidth, canvasHeight); // Update size
  camera.left = -canvasWidth/2;
  camera.right= canvasWidth/2;
  camera.top = canvasHeight/2;
  camera.bottom = -canvasHeight/2;
  camera.updateProjectionMatrix();
});

// Map Controls for Camera
const controls = new MapControls(camera, renderer.domElement);
controls.enableRotate = false;
controls.enableDamping = false;
controls.screenSpacePanning = true;

// Add GUI
// const stats = new Stats();
// document.getElementById('canvas-holder').appendChild( stats.dom );
const gui = new GUI({ autoPlace: false });
gui.close();
document.getElementById('dat-gui-holder').appendChild(gui.domElement);
// -------------------------------------------------------------------------------
// The dummy trapzoid, whose vertices' positions are determined in the vertex shader instead of here.
const trapzoidGeometry = new THREE.BufferGeometry(); 
const indices = [
	0, 1, 2,
	2, 3, 0,
];
trapzoidGeometry.setIndex(indices);

const Types = {
  Vanilla: 0,
  Stamp: 1,
  Airbrush: 2,
}
const StampModes = {
  EquiDistant: 0,
  RatioDistant: 1,
}
const strokeMaterial = new THREE.RawShaderMaterial( {
  uniforms: {
    type: {value: Types.Airbrush},
    alpha: {value: 1.0},
    color: {value: [0.0, 0.0, 0.0]},
    // Stamp
    footprint: {type: "t", value: new THREE.Texture()},
    stampInterval: {value: 1.0},
    noiseFactor: {value: 0.0},
    rotationFactor: {value: 0.0},
    stampMode: {value: StampModes.RatioDistant},
    // Airbrush
    gradient: { type: "t", value: new THREE.DataTexture() },
  },
  vertexShader: document.getElementById( 'vertexShader' ).textContent,
  fragmentShader: document.getElementById( 'fragmentShader' ).textContent,
  side: THREE.DoubleSide,
  transparent: true,
  glslVersion: THREE.GLSL3
} );

// The mesh object we upload everything to.
const polylineMesh = new THREE.InstancedMesh( trapzoidGeometry, strokeMaterial); 
polylineMesh.frustumCulled = false;

let variables = {
  nSegments: 32,
  color: [0.0, 0.0, 0.0],
  backgroundColor: "#FFFFFF",
  bezierControlPoint1: new THREE.Vector2(0.33, 1.0),
  bezierControlPoint2: new THREE.Vector2(0.66, 0.0),
};

function renewSineWave(){
  loadSineWave();
  articulatedLineCompute();
  updatePolylineMesh();
}

function renewModel(name){
  loadModel(name);
  articulatedLineCompute();
  updatePolylineMesh();
}

// Since we don't have geometry shader and compute shader, we have to replicate their behaviors in javascript.
// Push two successive vertices' (an edge's) infos into a single vertex, same as the "lines" input in a geometry shader.
// For the batch rendering, if isEndPoint true, don't connect to the next point (in the vertex shader).

// Global variables to store vertex attributes:
let position0 = [];
let position1 = [];
let radius0 = [];
let radius1 = [];
let summedLength0 = [];
let summedLength1 = [];
let isEndPoint0 = [];

function loadSineWave(){
  position0 = [];
  position1 = [];
  radius0 = [];
  radius1 = [];
  isEndPoint0 = [];
  let n = variables.nSegments;

  // gr is the golden ratio value
  const gr = (1 + Math.sqrt(5)) / 2; 
  const pi = Math.PI;
  const maxRadius = 0.33;
  
  for(let i = 0; i <= n; ++i){
    let a = 1.0 * i / n;
    let x =  -pi + (2 * pi * a);
    let y = 1.0 / gr * Math.sin(x);
    let r = Math.cos(x / 2.0) * maxRadius;

    // resize a little bit, ugly code but work!
    const reizeFactor = 0.5;
    x *= reizeFactor; y *= reizeFactor; r*= reizeFactor;
    position0.push(x, y);
    radius0.push(r);
    isEndPoint0.push(0);
    if(i != 0) {
      position1.push(x, y);
      radius1.push(r);
    }
  }
  isEndPoint0[isEndPoint0.length - 1] = 1;
}

const swtichModel = {};
const modelObject = {};
for(const name of modelNames){
  import('../models/' + name + '.js').then((module) => {
    modelObject[name] = module.default;
    swtichModel[name] = ()=>renewModel(name);
    modelFolder.add(swtichModel, name).name(name);
  })
}

function loadModel(name){
  position0 = [];
  position1 = [];
  radius0 = [];
  radius1 = [];
  isEndPoint0 = [];
  
  let data = modelObject[name];
  let n = data.length/3;
  for(let i = 0; i < n; ++i){
    const radiusFactor = 0.02;
    const stride = i*3;
    const x = data[stride];
    const y = - data[stride + 1]; // for some stupid reason, the data is reversed.
    const r = data[stride + 2] * radiusFactor;

    if(isNaN(x)){
      if(!isNaN(y) || !isNaN(r)) throw new Error(message || "Wrong Model Data");
      isEndPoint0[isEndPoint0.length - 1] = 1;
      continue;
    }
    position0.push(x, y);
    radius0.push(r);
    isEndPoint0.push(0);
    if(i != 0) {
      position1.push(x, y);
      radius1.push(r);
    }
  }
  isEndPoint0[isEndPoint0.length - 1] = 1;
}
// The length is supposed to be calculated in a compute shader with the prefix sum algorithm. WebGL don't support it.
// Though WebGPU supports compute shader, shen isn't familiar with it.
function articulatedLineCompute(){
  summedLength0 = [];
  summedLength1 = [];

  if(polylineMesh.material.uniforms.stampMode.value == StampModes.EquiDistant){
    var currLength = 0.0;
    summedLength0.push(currLength);
    for(let i = 0; i < radius1.length; ++i){
      const stride = 2*i;
      const p0 = new THREE.Vector2(position0[stride], position0[stride+1]);
      const p1 = new THREE.Vector2(position1[stride], position1[stride+1]);
      if(isEndPoint0[i]) currLength = 0.0;
      else currLength += p0.distanceTo(p1);
      summedLength0.push(currLength);
      summedLength1.push(currLength);
    }
  }
  // The method about RatioDistant is not published yet. 
  // There is calculus in it so you can see several weired formulas when it comes.
  // Ignore it temporily.
  if(polylineMesh.material.uniforms.stampMode.value == StampModes.RatioDistant){
    var currLength = 0.0;
    summedLength0.push(currLength);
    for(let i = 0; i < radius1.length; ++i){
      const stride = 2*i;
      const p0 = new THREE.Vector2(position0[stride], position0[stride+1]);
      const p1 = new THREE.Vector2(position1[stride], position1[stride+1]);
      let r0 = radius0[i];
      let r1 = radius1[i];

      // When raidus is zero index comes to infinity, which is avoided here.
      const tolerance = 1e-5;
      if(r0 <= 0 || r0/r1 < tolerance){
        r0 = tolerance * r1;
        radius0[i] = r0;
      }
      if(r1 <= 0 || r1/r0 < tolerance){
        r1 = tolerance * r0;
        radius1[i] = r1;
      }

      let l = p0.distanceTo(p1);

      if(isEndPoint0[i]) currLength = 0.0;
      else if(r0 <= 0.0 && r1 <= 0.0) currLength += 0.0;
      else if(r0 == r1) currLength += l/r0;
      else currLength += Math.log(r0/r1)/(r0 - r1) * l;
      summedLength0.push(currLength);
      summedLength1.push(currLength);
    }
  }
}

function updatePolylineMesh() {
  polylineMesh.geometry.setAttribute("position0", new THREE.InstancedBufferAttribute(new Float32Array(position0), 2));
  polylineMesh.geometry.setAttribute("radius0", new THREE.InstancedBufferAttribute(new Float32Array(radius0), 1));
  polylineMesh.geometry.setAttribute("summedLength0", new THREE.InstancedBufferAttribute(new Float32Array(summedLength0), 1));
  polylineMesh.geometry.setAttribute("isEndPoint0", new THREE.InstancedBufferAttribute(new Int32Array(isEndPoint0), 1));
  polylineMesh.geometry.setAttribute("position1", new THREE.InstancedBufferAttribute(new Float32Array(position1), 2));
  polylineMesh.geometry.setAttribute("radius1", new THREE.InstancedBufferAttribute(new Float32Array(radius1), 1));
  polylineMesh.geometry.setAttribute("summedLength1", new THREE.InstancedBufferAttribute(new Float32Array(summedLength1), 1));
  
  polylineMesh.count = radius1.length;
}

// point1 and point2 are the cubic bezier control point 1 and 2
const updateGradient = (point1, point2) => {
  let curve = new THREE.CubicBezierCurve(
    new THREE.Vector2(0.0, 1.0),
    point1,
    point2,
    new THREE.Vector2(1.0, 0.0),
  );
  
  const width = 256;
  const height = 1;
  const size = width * height;
  const data = new Uint8Array( 4 * size );
  const points = curve.getPoints( width * 2 );
  
  // Resample on the polyline generated from the points
  for (let i = 0; i < width; ++i){
    let x = i/width;
    for (let j = 0; j < width * 2 - 1; ++j){
      let p0 = points[j], p1 = points[j+1];
      if(x >= p0.x && x <= p1.x){
        let y = (p0.y * (p1.x - x) + p1.y * (x - p0.x))/(p1.x - p0.x);
        data[i*4] = Math.floor(y * 255);
      }
    }
  }

  polylineMesh.material.uniforms.gradient.value.dispose();
  const gradientTexture = new THREE.DataTexture(data, width, height);
  gradientTexture.needsUpdate = true;
  polylineMesh.material.uniforms.gradient.value = gradientTexture;
  
  scene.add(polylineMesh);
}

updateGradient(variables.bezierControlPoint1, variables.bezierControlPoint2);

// GUI
// Common parameters
gui.addColor(variables, "backgroundColor").name("Background").onChange(
  (value) => {
    variables.backgroundColor = value;
    renderer.setClearColor(value);
  } 
);
gui.addColor(variables, 'color').name("Stroke Color").onChange( 
  (value) => {
    let color = polylineMesh.material.uniforms.color.value;
    color[0] = value[0]/255.0; color[1] = value[1]/255.0; color[2] = value[2]/255.0;
  }
);

gui.add(polylineMesh.material.uniforms.alpha, 'value', 0.0, 1.0, 0.01).name("Opacity");
gui.add(variables, 'nSegments', 2, 64, 1).name("Segments Count").onChange(
  (value) => {
    variables.nSegments = value;
    updatePolylineMesh();
  }
);

// Type specific parameters
const strokeTypeFolder = gui.addFolder("Stroke Types");
const airbrushFolder = gui.addFolder("Airbrush Parameters");
const stampFolder = gui.addFolder("Stamp Parameters");
const modelFolder = gui.addFolder("Model Types");

const swtichStorke = {
  vanilla: () => swtichType(Types.Vanilla),
  splatter: () => {
    swtichType(Types.Stamp);
    let uniforms = polylineMesh.material.uniforms;
    uniforms.stampMode.value= StampModes.RatioDistant;
    uniforms.footprint.value = new THREE.TextureLoader().load("img/stamp1.png");
    uniforms.stampInterval.value = 0.4;
    uniforms.noiseFactor.value = 0.0;
    uniforms.rotationFactor.value = 1.0;
    stampFolder.updateDisplay();
  },
  pencil: () => {
    swtichType(Types.Stamp);
    let uniforms = polylineMesh.material.uniforms;
    uniforms.stampMode.value = StampModes.RatioDistant;
    uniforms.footprint.value = new THREE.TextureLoader().load("img/stamp2.png");
    uniforms.stampInterval.value = 0.4;
    uniforms.noiseFactor.value = 1.2;
    uniforms.rotationFactor.value = 0.75;
    stampFolder.updateDisplay();
  },
  dot: () => {
    swtichType(Types.Stamp);
    let uniforms = polylineMesh.material.uniforms;
    uniforms.stampMode.value = StampModes.RatioDistant;
    uniforms.footprint.value = new THREE.TextureLoader().load("img/stamp3.png");
    uniforms.stampInterval.value = 2.0;
    uniforms.noiseFactor.value = 0.0;
    uniforms.rotationFactor.value = 0.0;
    stampFolder.updateDisplay();
  },
  airbrush: () => swtichType(Types.Airbrush),
}

function swtichType(type){
  stampFolder.domElement.style.display = 'none';
  airbrushFolder.domElement.style.display = 'none';
  if(type == Types.Vanilla){
    polylineMesh.material.uniforms.type.value = Types.Vanilla;
  }
  if(type == Types.Stamp) {
    polylineMesh.material.uniforms.type.value = Types.Stamp;
    stampFolder.domElement.style.display = '';
  }
  if(type == Types.Airbrush) {
    polylineMesh.material.uniforms.type.value = Types.Airbrush;
    airbrushFolder.domElement.style.display = '';
  }
}

strokeTypeFolder.open();
strokeTypeFolder.add(swtichStorke, 'vanilla').name("Vanilla");
strokeTypeFolder.add(swtichStorke, 'splatter').name("Splatter");
strokeTypeFolder.add(swtichStorke, 'pencil').name("Pencil");
strokeTypeFolder.add(swtichStorke, 'dot').name("Dot");
strokeTypeFolder.add(swtichStorke, 'airbrush').name("Airbrush");
swtichStorke.vanilla();

// Stamp
const swtichStampMode = {
  equiDistant: () => {
    polylineMesh.material.uniforms.stampMode.value = StampModes.EquiDistant;
    articulatedLineCompute();
    updatePolylineMesh();
  },
  ratioDistant: () => {
    polylineMesh.material.uniforms.stampMode.value = StampModes.RatioDistant;
    articulatedLineCompute();
    updatePolylineMesh();
  }
}
stampFolder.open();
stampFolder.add(swtichStampMode, "equiDistant").name("Equidistant");
stampFolder.add(swtichStampMode, "ratioDistant").name("Ratiodistant");
stampFolder.add(polylineMesh.material.uniforms.stampInterval, 'value', 0.001, 2.0, 0.01).name("Interval");
stampFolder.add(polylineMesh.material.uniforms.rotationFactor, 'value', 0.0, 2.0, 0.01).name("Rotation");
stampFolder.add(polylineMesh.material.uniforms.noiseFactor, 'value', 0.0, 2.0, 0.01).name("Noise");

// Airbrush
airbrushFolder.open();
airbrushFolder.add(variables.bezierControlPoint1, 'x', 0.0, 1.0, 0.01).name("Point 1 X").onChange(
  (value) => {
    variables.bezierControlPoint1.x = value;
    updateGradient(variables.bezierControlPoint1, variables.bezierControlPoint2);
  }
)
airbrushFolder.add(variables.bezierControlPoint1, 'y', 0.0, 1.0, 0.01).name("Point 1 Y").onChange(
  (value) => {
    variables.bezierControlPoint1.y = value;
    updateGradient(variables.bezierControlPoint1, variables.bezierControlPoint2);
  }
);

airbrushFolder.add(variables.bezierControlPoint2, 'x', 0.0, 1.0, 0.01).name("Point 2 X").onChange(
  (value) => {
    variables.bezierControlPoint2.x = value;
    updateGradient(variables.bezierControlPoint1, variables.bezierControlPoint2);
  }
);

airbrushFolder.add(variables.bezierControlPoint2, 'y', 0.0, 1.0, 0.01).name("Point 2 Y").onChange(
  (value) => {
    variables.bezierControlPoint2.y = value;
    updateGradient(variables.bezierControlPoint1, variables.bezierControlPoint2);
  }
);

// Geometry
swtichModel.sineWave = ()=>renewSineWave();
modelFolder.add(swtichModel, "sineWave").name("Sine Wave");
modelFolder.open();
// The default geometry
swtichModel.sineWave();

// -------------------------------------------------------------------------------
// Rendering Function
let guiClosed = gui.closed;
const rendering = function () {
  // Rerender every time the page refreshes (pause when on another tab)
  requestAnimationFrame(rendering);
  controls.update();
  if ( guiClosed != gui.closed ){
    if(!guiClosed){
        document.getElementById('dat-gui-holder').style.left = "0em"
    }else{
        document.getElementById('dat-gui-holder').style.left = (-gui.width).toString() + "px";
    }
    guiClosed = gui.closed;
  }
  // stats.update();
  renderer.render(scene, camera);
};

rendering();