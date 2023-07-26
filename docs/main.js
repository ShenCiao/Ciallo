import * as THREE from 'three';
import { MapControls } from "three/addons/controls/MapControls.js";
import Stats from 'three/addons/libs/stats.module'
import { GUI } from 'three/addons/libs/lil-gui.module.min.js'

// Scene
const scene = new THREE.Scene();

// Camera
let camera = new THREE.OrthographicCamera( 
  window.innerWidth / - 2, window.innerWidth / 2, 
  window.innerHeight / 2, window.innerHeight / - 2,
  0.0, 1000
  );
camera.position.z = 1;
camera.zoom = 100;
camera.updateProjectionMatrix();

// Renderer
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setClearColor("#233143"); // Set background colour
renderer.setSize(window.innerWidth, window.innerHeight);
document.body.appendChild(renderer.domElement); // Add renderer to HTML as a canvas element

// Make Canvas Responsive
window.addEventListener("resize", () => {
  renderer.setSize(window.innerWidth, window.innerHeight); // Update size
  camera.left = -window.innerWidth/2;
  camera.right= window.innerWidth/2;
  camera.top = window.innerHeight/2;
  camera.bottom = -window.innerHeight/2;
  camera.updateProjectionMatrix();
});

// Trackball Controls for Camera
const controls = new MapControls(camera, renderer.domElement);
controls.enableRotate = false;
controls.enableDamping = false;
controls.screenSpacePanning = true;

// Add GUI
const stats = new Stats();
document.body.appendChild(stats.dom);
const gui = new GUI();
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
const strokeMaterial = new THREE.RawShaderMaterial( {
  uniforms: {
    type: {value: Types.Vanilla},
    alpha: {value: 1.0}, // it's pretty annoying threejs don't support RGBA color.
    color: new THREE.Uniform(new THREE.Color()),
  },
  vertexShader: document.getElementById( 'vertexShader' ).textContent,
  fragmentShader: document.getElementById( 'fragmentShader' ).textContent,
  side: THREE.DoubleSide,
  transparent: true,
  glslVersion: THREE.GLSL3
} );

// I mean "polylinePath" here but in threejs it's a mesh object, hope it won't confuse you.
const polylineMesh = new THREE.InstancedMesh( trapzoidGeometry, strokeMaterial); 
polylineMesh.frustumCulled = false; // I debuged half hours to find this property.

// Since we don't have geometry shader and compute shader, we have to replicate their behaviors here.
// Push two successive vertices' (an edge's) infos into a single vertex, same as the "lines" input in geometry shader.
const updatePolylineMesh = (n) => {
  polylineMesh.geometry.deleteAttribute("position0");
  polylineMesh.geometry.deleteAttribute("radius0");
  polylineMesh.geometry.deleteAttribute("position1");
  polylineMesh.geometry.deleteAttribute("radius1");
  const position0 = [];
  const position1 = [];
  const radius0 = [];
  const radius1 = [];

  // golden ratio
  const gr = (1 + Math.sqrt(5)) / 2; 
  const pi = Math.PI;
  const maxRadius = 0.33;
  for(let i = 0; i <= n; ++i){
    let a = 1.0 * i / n;
    let x =  -pi + (2 * pi * a);
    let y = 1.0 / gr * Math.sin(x);
    let r = Math.cos(x / 2.0) * maxRadius;
    position0.push(x, y);
    radius0.push(r);
    if(i != 0) {
      position1.push(x, y);
      radius1.push(r);
    }
  }

  polylineMesh.geometry.setAttribute("position0", new THREE.InstancedBufferAttribute(new Float32Array(position0), 2));
  polylineMesh.geometry.setAttribute("radius0", new THREE.InstancedBufferAttribute(new Float32Array(radius0), 1));
  polylineMesh.geometry.setAttribute("position1", new THREE.InstancedBufferAttribute(new Float32Array(position1), 2));
  polylineMesh.geometry.setAttribute("radius1", new THREE.InstancedBufferAttribute(new Float32Array(radius1), 1));
  polylineMesh.count = n;
}

let variables = {
  nSegments: 32,
};

updatePolylineMesh(variables.nSegments);
scene.add(polylineMesh);

gui.addColor(polylineMesh.material.uniforms, 'color').name("RGB").onChange( 
  (value) => polylineMesh.material.uniforms.color.value = value 
);
gui.add(polylineMesh.material.uniforms.alpha, 'value', 0.0, 1.0, 0.01).name("Opacity");
gui.add(variables, 'nSegments', 2, 64, 1).name("Segments Count").onChange(
  (value) => updatePolylineMesh(value)
);
// -------------------------------------------------------------------------------
// Rendering Function
const rendering = function () {
  // Rerender every time the page refreshes (pause when on another tab)
  requestAnimationFrame(rendering);
  controls.update();
  stats.update();
  renderer.render(scene, camera);
};

rendering();