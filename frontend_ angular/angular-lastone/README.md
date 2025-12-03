# Smart Career Path Recommender - Angular Version

This is the Angular conversion of the React project. The project has been fully converted from React to Angular with the following changes:

## Key Changes

### 1. Project Structure
- Converted from Vite + React to Angular CLI
- All components are now standalone Angular components
- Routing uses Angular Router instead of state-based navigation

### 2. State Management
- React `useState` hooks replaced with Angular services (`UserDataService`)
- State is managed through RxJS `BehaviorSubject` and `Observable`
- Components subscribe to state changes using Angular's reactive patterns

### 3. Components
- All React components (`.tsx`) converted to Angular components (`.ts` + `.html` + `.css`)
- React hooks (`useState`, `useEffect`, `useRef`) replaced with Angular lifecycle hooks (`ngOnInit`, `ngOnDestroy`)
- JSX syntax converted to Angular template syntax (`*ngFor`, `*ngIf`, property/event bindings)

### 4. Routing
- State-based navigation replaced with Angular Router
- Routes defined in `src/app/app.routes.ts`
- Navigation handled through `Router.navigate()`

### 5. Animations
- Motion library replaced with Angular Animations
- Animation triggers defined in component decorators
- Uses Angular's `@angular/animations` module

### 6. UI Components
- All UI components converted to Angular standalone components
- Preserved all styling (Tailwind CSS classes)
- Component structure maintained

### 7. Dependencies
- React dependencies removed
- Angular dependencies added:
  - `@angular/core`, `@angular/common`, `@angular/router`, etc.
  - `lucide-angular` (replaces `lucide-react`)
  - `clsx` and `tailwind-merge` (kept for utility functions)

## Installation

1. Navigate to the angular folder:
```bash
cd angular
```

2. Install dependencies:
```bash
npm install
```

3. Run the development server:
```bash
npm start
# or
ng serve
```

4. Build for production:
```bash
npm run build
# or
ng build
```

## Project Structure

```
angular/
├── src/
│   ├── app/
│   │   ├── components/
│   │   │   ├── ui/              # UI components (button, input, card, etc.)
│   │   │   ├── navigation/      # Navigation component
│   │   │   ├── footer/          # Footer component
│   │   │   ├── registration-page/
│   │   │   ├── interests-page/
│   │   │   ├── personality-quiz/
│   │   │   ├── recommendations-page/
│   │   │   ├── features-hub/
│   │   │   ├── cv-builder-feature/
│   │   │   ├── job-parser-feature/
│   │   │   └── ai-interviewer-feature/
│   │   ├── services/
│   │   │   └── user-data.service.ts  # State management service
│   │   ├── app.component.ts     # Main app component
│   │   ├── app.routes.ts        # Routing configuration
│   │   └── app.config.ts        # App configuration
│   ├── styles/
│   │   └── globals.css          # Global styles
│   ├── index.html
│   ├── main.ts                   # Application entry point
│   └── styles.css                # Main styles file
├── angular.json
├── tsconfig.json
├── tsconfig.app.json
├── package.json
├── tailwind.config.js
└── postcss.config.js
```

## Key Features

- ✅ All React components converted to Angular
- ✅ Routing implemented with Angular Router
- ✅ State management with Angular services
- ✅ Animations using Angular Animations
- ✅ Tailwind CSS styling preserved
- ✅ All functionality maintained

## Notes

- Some UI components (like dropdown menus) have been simplified but maintain the same functionality
- The project uses Angular standalone components (no NgModules)
- All components are lazy-loaded through routing
- The project is ready to run with `ng serve` from the `angular` folder
