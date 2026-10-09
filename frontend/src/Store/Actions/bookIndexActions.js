import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { filterBuilderTypes, filterBuilderValueTypes, filterTypePredicates, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import sortByName from 'Utilities/Array/sortByName';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import { set, updateItem } from './baseActions';
import { filterPredicates, filters, sortPredicates } from './bookActions';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionFilterReducer from './Creators/Reducers/createSetClientSideCollectionFilterReducer';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'bookIndex';

//
// State

export const defaultState = {
  isSaving: false,
  saveError: null,
  isDeleting: false,
  deleteError: null,
  sortKey: 'title',
  sortDirection: sortDirections.ASCENDING,
  secondarySortKey: 'title',
  secondarySortDirection: sortDirections.ASCENDING,
  view: 'posters',

  posterOptions: {
    detailedProgressBar: false,
    size: 'large',
    showTitle: true,
    showAuthor: true,
    showMonitored: true,
    showQualityProfile: true,
    showSearchAction: false
  },

  overviewOptions: {
    detailedProgressBar: false,
    size: 'medium',
    showReleaseDate: true,
    showMonitored: true,
    showQualityProfile: true,
    showAdded: false,
    showPath: false,
    showSizeOnDisk: false,
    showSearchAction: false
  },

  tableOptions: {
    showSearchAction: false
  },

  columns: [
    {
      name: 'select',
      columnLabel: () => translate('Select'),
      isSortable: false,
      isVisible: true,
      isModifiable: false,
      isHidden: true
    },
    {
      name: 'status',
      columnLabel: () => translate('Status'),
      isSortable: true,
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'title',
      label: () => translate('Book'),
      isSortable: true,
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'authorName',
      label: () => translate('Author'),
      isSortable: true,
      isVisible: true,
      isModifiable: true
    },
    {
      name: 'narrator',
      label: () => translate('Narrator'),
      isSortable: true,
      isVisible: true,
      isModifiable: true
    },
    {
      name: 'duration',
      label: () => translate('Duration'),
      isSortable: true,
      isVisible: false,
      isModifiable: true
    },
    {
      name: 'releaseDate',
      label: () => translate('ReleaseDate'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'qualityProfileId',
      label: () => translate('QualityProfile'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'added',
      label: () => translate('Added'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'bookFileCount',
      label: () => translate('BookFileCount'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'availability',
      label: () => translate('Availability'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'fileFormats',
      label: () => translate('Formats'),
      isSortable: false,
      isVisible: false
    },
    {
      name: 'languages',
      label: () => translate('Languages'),
      isSortable: false,
      isVisible: false
    },
    {
      name: 'path',
      label: () => translate('Path'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'sizeOnDisk',
      label: () => translate('SizeOnDisk'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'genres',
      label: () => translate('Genres'),
      isSortable: false,
      isVisible: false
    },
    {
      name: 'ratings',
      label: () => translate('Rating'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'tags',
      label: () => translate('Tags'),
      isSortable: false,
      isVisible: false
    },
    {
      name: 'actions',
      columnLabel: () => translate('Actions'),
      isVisible: true,
      isModifiable: false
    }
  ],

  sortPredicates: {
    ...sortPredicates,

    authorName: function(item) {
      return item.author.sortName;
    },

    narrator: function(item) {
      return item.narrator || 'zzz'; // Put empty/null narrators at the end
    },

    duration: function(item) {
      return item.duration || '00:00:00'; // Put empty/null durations at the beginning
    },

    bookFileCount: function(item) {
      const { statistics = {} } = item;

      return statistics.bookFileCount || 0;
    },

    availability: function(item) {
      const { availability } = item;

      // listen=1, read=2, both=3, none/unknown=0
      return (availability?.canListen ? 1 : 0) + (availability?.canRead ? 2 : 0);
    },

    ratings: function(item) {
      const { ratings = {} } = item;

      return ratings.value;
    }
  },

  selectedFilterKey: 'all',

  filters,

  filterPredicates: {
    ...filterPredicates,

    author: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      return predicate(item.author.authorName, filterValue);
    },

    narrator: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      return predicate(item.narrator || '', filterValue);
    },

    anyEditionOk: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      return predicate(item.anyEditionOk, filterValue);
    },

    canRead: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      // Unknown availability must not match either true or false.
      if (!item.availability) {
        return false;
      }

      return predicate(item.availability.canRead === true, filterValue);
    },

    canListen: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      // Unknown availability must not match either true or false.
      if (!item.availability) {
        return false;
      }

      return predicate(item.availability.canListen === true, filterValue);
    },

    fileFormats: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      return predicate(item.availability?.fileFormats ?? [], filterValue);
    },

    languages: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      return predicate(item.availability?.languages ?? [], filterValue);
    }
  },

  filterBuilderProps: [
    {
      name: 'author',
      label: () => translate('Author'),
      type: filterBuilderTypes.STRING
    },
    {
      name: 'narrator',
      label: () => translate('Narrator'),
      type: filterBuilderTypes.STRING
    },
    {
      name: 'title',
      label: () => translate('Title'),
      type: filterBuilderTypes.STRING
    },
    {
      name: 'monitored',
      label: () => translate('Monitored'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'anyEditionOk',
      label: () => translate('AutomaticReleaseSwitching'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'qualityProfileId',
      label: () => translate('QualityProfile'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.QUALITY_PROFILE
    },
    {
      name: 'releaseDate',
      label: () => translate('ReleaseDate'),
      type: filterBuilderTypes.DATE,
      valueType: filterBuilderValueTypes.DATE
    },
    {
      name: 'added',
      label: () => translate('Added'),
      type: filterBuilderTypes.DATE,
      valueType: filterBuilderValueTypes.DATE
    },
    {
      name: 'bookFileCount',
      label: () => translate('BookFileCount'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'path',
      label: () => translate('Path'),
      type: filterBuilderTypes.STRING
    },
    {
      name: 'sizeOnDisk',
      label: () => translate('SizeOnDisk'),
      type: filterBuilderTypes.NUMBER,
      valueType: filterBuilderValueTypes.BYTES
    },
    {
      name: 'canRead',
      label: () => translate('CanRead'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'canListen',
      label: () => translate('CanListen'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'fileFormats',
      label: () => translate('Formats'),
      type: filterBuilderTypes.ARRAY,
      optionsSelector: function(items) {
        const values = new Set();

        items.forEach((book) => {
          (book.availability?.fileFormats ?? []).forEach((value) => values.add(value));
        });

        return Array.from(values).sort().map((value) => ({ id: value, name: value }));
      }
    },
    {
      name: 'languages',
      label: () => translate('Languages'),
      type: filterBuilderTypes.ARRAY,
      optionsSelector: function(items) {
        const values = new Set();

        items.forEach((book) => {
          (book.availability?.languages ?? []).forEach((value) => values.add(value));
        });

        return Array.from(values).sort().map((value) => ({ id: value, name: value }));
      }
    },
    {
      name: 'genres',
      label: () => translate('Genres'),
      type: filterBuilderTypes.ARRAY,
      optionsSelector: function(items) {
        const tagList = items.reduce((acc, Book) => {
          Book.genres.forEach((genre) => {
            acc.push({
              id: genre,
              name: genre
            });
          });

          return acc;
        }, []);

        return tagList.sort(sortByName);
      }
    },
    {
      name: 'ratings',
      label: () => translate('Rating'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'tags',
      label: () => translate('Tags'),
      type: filterBuilderTypes.ARRAY,
      valueType: filterBuilderValueTypes.TAG
    }
  ]
};

export const persistState = [
  'bookIndex.sortKey',
  'bookIndex.sortDirection',
  'bookIndex.selectedFilterKey',
  'bookIndex.customFilters',
  'bookIndex.view',
  'bookIndex.columns',
  'bookIndex.posterOptions',
  'bookIndex.bannerOptions',
  'bookIndex.overviewOptions',
  'bookIndex.tableOptions'
];

//
// Actions Types

export const SET_BOOK_SORT = 'bookIndex/setBookSort';
export const SET_BOOK_FILTER = 'bookIndex/setBookFilter';
export const SET_BOOK_VIEW = 'bookIndex/setBookView';
export const SET_BOOK_TABLE_OPTION = 'bookIndex/setBookTableOption';
export const SET_BOOK_POSTER_OPTION = 'bookIndex/setBookPosterOption';
export const SET_BOOK_BANNER_OPTION = 'bookIndex/setBookBannerOption';
export const SET_BOOK_OVERVIEW_OPTION = 'bookIndex/setBookOverviewOption';
export const SAVE_BOOK_EDITOR = 'bookEditor/saveBookEditor';
export const BULK_DELETE_BOOK = 'bookEditor/bulkDeleteBook';

//
// Action Creators

export const setBookSort = createAction(SET_BOOK_SORT);
export const setBookFilter = createAction(SET_BOOK_FILTER);
export const setBookView = createAction(SET_BOOK_VIEW);
export const setBookTableOption = createAction(SET_BOOK_TABLE_OPTION);
export const setBookPosterOption = createAction(SET_BOOK_POSTER_OPTION);
export const setBookBannerOption = createAction(SET_BOOK_BANNER_OPTION);
export const setBookOverviewOption = createAction(SET_BOOK_OVERVIEW_OPTION);
export const saveBookEditor = createThunk(SAVE_BOOK_EDITOR);
export const bulkDeleteBook = createThunk(BULK_DELETE_BOOK);

//
// Action Handlers

export const actionHandlers = handleThunks({
  [SAVE_BOOK_EDITOR]: function(getState, payload, dispatch) {
    dispatch(set({
      section,
      isSaving: true
    }));

    const promise = createAjaxRequest({
      url: '/book/editor',
      method: 'PUT',
      data: JSON.stringify(payload),
      dataType: 'json'
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        ...data.map((book) => {
          return updateItem({
            id: book.id,
            section: 'books',
            ...book
          });
        }),

        set({
          section,
          isSaving: false,
          saveError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isSaving: false,
        saveError: xhr
      }));
    });
  },

  [BULK_DELETE_BOOK]: function(getState, payload, dispatch) {
    dispatch(set({
      section,
      isDeleting: true
    }));

    const promise = createAjaxRequest({
      url: '/book/editor',
      method: 'DELETE',
      data: JSON.stringify(payload),
      dataType: 'json'
    }).request;

    promise.done(() => {
      // SignalR will take care of removing the book from the collection

      dispatch(set({
        section,
        isDeleting: false,
        deleteError: null
      }));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isDeleting: false,
        deleteError: xhr
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_BOOK_SORT]: createSetClientSideCollectionSortReducer(section),
  [SET_BOOK_FILTER]: createSetClientSideCollectionFilterReducer(section),

  [SET_BOOK_VIEW]: function(state, { payload }) {
    return Object.assign({}, state, { view: payload.view });
  },

  [SET_BOOK_TABLE_OPTION]: createSetTableOptionReducer(section),

  [SET_BOOK_POSTER_OPTION]: function(state, { payload }) {
    const posterOptions = state.posterOptions;

    return {
      ...state,
      posterOptions: {
        ...posterOptions,
        ...payload
      }
    };
  },

  [SET_BOOK_BANNER_OPTION]: function(state, { payload }) {
    const bannerOptions = state.bannerOptions;

    return {
      ...state,
      bannerOptions: {
        ...bannerOptions,
        ...payload
      }
    };
  },

  [SET_BOOK_OVERVIEW_OPTION]: function(state, { payload }) {
    const overviewOptions = state.overviewOptions;

    return {
      ...state,
      overviewOptions: {
        ...overviewOptions,
        ...payload
      }
    };
  }

}, defaultState, section);
