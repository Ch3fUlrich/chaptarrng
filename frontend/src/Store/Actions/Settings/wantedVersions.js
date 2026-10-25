import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveHandler from 'Store/Actions/Creators/createSaveHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.wantedVersions';

//
// Actions Types

export const FETCH_WANTED_VERSIONS = 'FETCH_WANTED_VERSIONS';
export const SET_WANTED_VERSIONS_VALUE = 'SET_WANTED_VERSIONS_VALUE';
export const SAVE_WANTED_VERSIONS = 'SAVE_WANTED_VERSIONS';

//
// Action Creators

export const fetchWantedVersions = createThunk(FETCH_WANTED_VERSIONS);
export const saveWantedVersions = createThunk(SAVE_WANTED_VERSIONS);
export const setWantedVersionsValue = createAction(SET_WANTED_VERSIONS_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Details

export default {

  //
  // State

  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    pendingChanges: {},
    isSaving: false,
    saveError: null,
    item: {}
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_WANTED_VERSIONS]: createFetchHandler(section, '/config/wantedversions'),
    [SAVE_WANTED_VERSIONS]: createSaveHandler(section, '/config/wantedversions')
  },

  //
  // Reducers

  reducers: {
    [SET_WANTED_VERSIONS_VALUE]: createSetSettingValueReducer(section)
  }

};
